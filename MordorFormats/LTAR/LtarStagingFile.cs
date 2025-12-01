using Edoke.IO;
using MordorFormats.Compression;
using OodleCoreSharp;
using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace MordorFormats.LTAR
{
    /// <summary>
    /// A file used for staging in writing Lithtech archives.
    /// </summary>
    public class LtarStagingFile
    {
        /// <summary>
        /// The max uncompressed size of chunks.
        /// </summary>
        private const int MaxChunkSize = 65536;

        /// <summary>
        /// The padding value chunks use.
        /// </summary>
        private const byte ChunkPadValue = 0x58;

        /// <summary>
        /// The name of the file, filled later.
        /// </summary>
        internal string Name { get; set; }

        /// <summary>
        /// The full file path of this file, to be loaded later when writing.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// The flags of this file, unknown purpose, always seems to be 9.
        /// </summary>
        public int Flags { get; set; }

        /// <summary>
        /// Creates a new <see cref="LtarStagingFile"/>.
        /// </summary>
        /// <param name="filePath">The full file path to load later when writing.</param>
        /// <param name="flags">The flags of the file, unknown purpose, always seems to be 9.</param>
        public LtarStagingFile(string filePath, int flags = 9)
        {
            Name = string.Empty;
            FilePath = filePath;
            Flags = flags;
        }

        /// <summary>
        /// Writes a file entry for this file.
        /// </summary>
        /// <param name="bw">The stream writer.</param>
        /// <param name="version">The version of the lithtech archive.</param>
        /// <param name="nameOffset">The offset to the name of the file entry.</param>
        /// <param name="index">The index of the file entry.</param>
        internal void Write(BinaryStreamWriter bw, int version, int nameOffset, int index)
        {
            bw.WriteInt32(nameOffset);
            bw.ReserveInt64($"FileOffset_{index}");
            bw.ReserveInt64($"FileCompressedSize_{index}");
            bw.ReserveInt64($"FileSize_{index}");

            if (version >= 4)
            {
                bw.WriteByte(1);
                bw.WriteByte((byte)Flags);
            }
            else
            {
                bw.WriteInt32(Flags);
            }
        }

        /// <summary>
        /// Writes the data of the file entry for this staging file.
        /// </summary>
        /// <param name="bw">The stream writer.</param>
        /// <param name="version">The version of the lithtech archive.</param>
        /// <param name="index">The index of the file entry.</param>
        /// <param name="zlibSmallest">Whether or not to prefer smallest compression for zlib if applicable.</param>
        /// <param name="compressor">The oodle compressor to use for this file if applicable.</param>
        /// <param name="level">The oodle compression level to use for this file if applicable.</param>
        internal void WriteData(BinaryStreamWriter bw, int version, int index, bool zlibSmallest, OodleLZ_Compressor compressor, OodleLZ_CompressionLevel level)
        {
            long fileOffset = bw.Position;
            bw.FillInt64($"FileOffset_{index}", fileOffset);

            using var fs = OpenFile();
            long fileSize = fs.Length;
            bw.FillInt64($"FileSize_{index}", fileSize);

            Compress(fs, bw, version, fileOffset, fileSize, zlibSmallest, compressor, level);

            long fileEndOffset = bw.Position;
            long fileCompressedSize = fileEndOffset - fileOffset;
            bw.FillInt64($"FileCompressedSize_{index}", fileCompressedSize);
        }

        /// <summary>
        /// Wraps opening the file for this staging file, checking if it exists first.
        /// </summary>
        /// <returns>The open file.</returns>
        /// <exception cref="FileNotFoundException">The file could not be opened.</exception>
        private FileStream OpenFile()
        {
            var fi = new FileInfo(FilePath);
            if (!fi.Exists)
            {
                throw new FileNotFoundException($"Could not open staging file for writing: \"{FilePath}\"");
            }

            return fi.OpenRead();
        }

        /// <summary>
        /// Compresses the specified file stream.
        /// </summary>
        /// <param name="fs">The file to compress.</param>
        /// <param name="bw">The stream writer.</param>
        /// <param name="version">The version of the lithtech archive.</param>
        /// <param name="fileOffset">The offset of the file within the lithtech archive.</param>
        /// <param name="size">The original size of the file.</param>
        /// <param name="zlibSmallest">Whether or not to prefer smallest compression for zlib if applicable.</param>
        /// <param name="compressor">The oodle compressor to use for this file if applicable.</param>
        /// <param name="level">The oodle compression level to use for this file if applicable.</param>
        private static void Compress(FileStream fs, BinaryStreamWriter bw, int version, long fileOffset, long size, bool zlibSmallest, OodleLZ_Compressor compressor, OodleLZ_CompressionLevel level)
        {
            long remaining = size;

            // Rent read and write buffers
            byte[] rawBuf = ArrayPool<byte>.Shared.Rent(MaxChunkSize * 2);
            Span<byte> rawBufSpan = rawBuf.AsSpan();
            Span<byte> rawReadBuf = rawBufSpan[..MaxChunkSize];
            Span<byte> rawWriteBuf = rawBufSpan.Slice(MaxChunkSize, MaxChunkSize);

            // Keep writing until all data is compressed and written
            while (remaining > 0)
            {
                // Get the next chunk size, 65536 or remaining, whichever is smaller
                int chunkSize = (int)Math.Min(remaining, MaxChunkSize);

                // Setup read buffer
                Span<byte> readBuf = rawReadBuf[..chunkSize];

                // Read data
                // Then compress it
                fs.ReadExactly(readBuf);
                int compChunkSize = CompressChunk(readBuf, rawWriteBuf, version, zlibSmallest, compressor, level);
                if (compChunkSize >= chunkSize)
                {
                    // If compressed size is greater than or equal to the original, just write the original
                    compChunkSize = chunkSize;
                    bw.WriteInt32(compChunkSize);
                    bw.WriteInt32(chunkSize);
                    bw.WriteByteSpan(readBuf);
                }
                else
                {
                    // Setup write buffer
                    // Then write
                    Span<byte> writeBuf = rawWriteBuf[..compChunkSize];
                    bw.WriteInt32(compChunkSize);
                    bw.WriteInt32(chunkSize);
                    bw.WriteByteSpan(writeBuf);
                }

                // Align by 4 relative to the start of all chunks
                bw.PadRelative(fileOffset, 4, ChunkPadValue);

                // Decrement written count from remaining
                remaining -= chunkSize;
            }

            // Make sure to return the rented read and write buffers
            ArrayPool<byte>.Shared.Return(rawBuf);
        }

        /// <summary>
        /// Compress the specified chunk.
        /// </summary>
        /// <param name="source">The data to compress.</param>
        /// <param name="dest">The destination to compress to; Currently will not be written to if compressed size is larger than or equal to original size.</param>
        /// <param name="version">The version of the lithtech archive.</param>
        /// <param name="zlibSmallest">Whether or not to prefer smallest compression for zlib if applicable.</param>
        /// <param name="compressor">The oodle compressor to use for this chunk if applicable.</param>
        /// <param name="level">The oodle compression level to use for this chunk if applicable.</param>
        /// <returns>The amount compressed.</returns>
        /// <exception cref="NotSupportedException">The version was unknown.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompressChunk(ReadOnlySpan<byte> source, Span<byte> dest, int version, bool zlibSmallest, OodleLZ_Compressor compressor, OodleLZ_CompressionLevel level)
        {
            int compChunkSize;
            switch (version)
            {
                case 3:
                    // Compress with zlib
                    compChunkSize = Zlib.Compress(source, dest, zlibSmallest ? CompressionLevel.SmallestSize : Deflate.DefaultCompressionLevel, 0x78, 0xDA);
                    break;
                case 4:
                    // Compress with oodle
                    var oodle = Oodle.GetOodleCompressor();
                    int sizeNeeded = (int)oodle.GetCompressedBufferSizeNeeded(compressor, source.Length);

                    byte[] compBuf = ArrayPool<byte>.Shared.Rent(sizeNeeded);
                    compChunkSize = (int)oodle.Compress(compressor, source, compBuf, level);
                    if (compChunkSize < MaxChunkSize)
                    {
                        // Kind of hacky but should work
                        compBuf.AsSpan()[..compChunkSize].CopyTo(dest);
                    }

                    break;
                default:
                    // Unknown file version
                    throw new NotSupportedException($"Unknown file version for chunk compression: {version}");
            }

            return compChunkSize;
        }

        public override string ToString()
        {
            return FilePath;
        }
    }
}

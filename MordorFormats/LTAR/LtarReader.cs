using Edoke.IO;
using MordorFormats.Compression;
using OodleCoreSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

namespace MordorFormats.LTAR
{
    /// <summary>
    /// A reader for lithtech archives.
    /// </summary>
    public class LtarReader : IDisposable
    {
        /// <summary>
        /// The max uncompressed size of chunks.
        /// </summary>
        private const int MaxChunkSize = 65536;

        /// <summary>
        /// The offset paths always begin at.
        /// </summary>
        private const int PathsOffset = 48;

        /// <summary>
        /// The underlying stream reader.
        /// </summary>
        private readonly BinaryStreamReader Reader;

        /// <summary>
        /// Whether or not this reader has been disposed.
        /// </summary>
        private bool disposedValue;

        /// <summary>
        /// Whether or not this lithtech archive is big endian.
        /// </summary>
        public bool BigEndian { get; set; }

        /// <summary>
        /// The version of this lithtech archive.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Unknown; Always 1.
        /// </summary>
        public int Unk14 { get; set; }

        /// <summary>
        /// The files contained within this lithtech archive.
        /// </summary>
        public List<LtarFile> Files { get; set; }

        /// <summary>
        /// The folders contained within this lithtech archive.
        /// </summary>
        public List<LtarFolder> Folders { get; set; }

        /// <summary>
        /// Reads a lithtech archive from a stream.
        /// </summary>
        /// <param name="br">The stream reader.</param>
        private LtarReader(BinaryStreamReader br)
        {
            Reader = br;
            string magic = br.AssertASCII(["LTAR", "RATL"]);
            if (magic == "RATL")
                BigEndian = br.BigEndian = true;
            else
                BigEndian = br.BigEndian = false;

            Version = br.ReadInt32();
            int pathsSize = br.ReadInt32();
            int folderCount = br.ReadInt32();
            int fileCount = br.ReadInt32();
            Unk14 = br.ReadInt32();
            br.AssertPattern(24, 0);

            br.Seek(PathsOffset + pathsSize);
            Files = new List<LtarFile>(fileCount);
            for (int i = 0; i < fileCount; i++)
            {
                Files.Add(new LtarFile(br, Version));
            }

            Folders = new List<LtarFolder>(folderCount);
            for (int i = 0; i < folderCount; i++)
            {
                Folders.Add(new LtarFolder(br));
            }
        }

        #region Is

        /// <summary>
        /// Whether or not the specified data is detected as a lithtech archive.
        /// </summary>
        /// <param name="br">The stream reader.</param>
        /// <returns>Whether or not the specified data is detected as a lithtech archive.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Is(BinaryStreamReader br)
        {
            string magic = br.GetASCII(0, 4);
            return magic == "LTAR" || magic == "RATL";
        }

        /// <summary>
        /// Whether or not the specified data is detected as a lithtech archive.
        /// </summary>
        /// <param name="path">The file path to the data.</param>
        /// <returns>Whether or not the specified data is detected as a lithtech archive.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Is(string path)
        {
            using var br = new BinaryStreamReader(path);
            return Is(br);
        }

        #endregion

        #region IsRead

        /// <summary>
        /// Reads the specified file as a lithtech archive if it is detected as one.
        /// </summary>
        /// <param name="path">The path to read from.</param>
        /// <param name="reader">The opened reader if applicable.</param>
        /// <returns>Whether or not the specified data was detected as a lithtech archive, and a reader opened.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRead(string path, [NotNullWhen(true)] out LtarReader? reader)
        {
            var br = new BinaryStreamReader(path);
            if (Is(br))
            {
                reader = new LtarReader(br);
                return true;
            }

            br.Dispose();
            reader = null;
            return false;
        }

        #endregion

        #region Read

        /// <summary>
        /// Reads a lithtech archive from the specified file path.
        /// </summary>
        /// <param name="path">The file path to open for reading as a lithtech archive.</param>
        /// <returns>A reader for a lithtech archive.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LtarReader Read(string path)
            => new LtarReader(new BinaryStreamReader(path));

        #endregion

        #region ReadFile

        /// <summary>
        /// Reads the specified file entry and decompresses it.<br/>
        /// Using this method is not recommended, prefer the streaming method as files may be too large.
        /// </summary>
        /// <param name="file">The file entry.</param>
        /// <returns>The decompressed file data.</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public byte[] ReadFile(LtarFile file)
        {
            if (file.CompressedSize > int.MaxValue || file.Size > int.MaxValue)
            {
                throw new InvalidOperationException("File is too large to be read or written to a 32-bit byte array, use the stream reading method instead.");
            }

            Reader.Seek(file.Offset);

            byte[] compressedBytes = Reader.ReadBytes((int)file.CompressedSize);
            byte[] decompressedBytes = new byte[file.Size];
            Decompress(compressedBytes, decompressedBytes, BigEndian);
            return decompressedBytes;
        }

        /// <summary>
        /// Reads the specified file entry and decompresses it to the specified output stream.
        /// </summary>
        /// <param name="file">The file entry.</param>
        /// <param name="output">The output stream.</param>
        public void ReadFile(LtarFile file, Stream output)
        {
            Reader.Seek(file.Offset);

            using var bw = new BinaryStreamWriter(output, BigEndian);
            Decompress(Reader, bw, file.Offset, file.Size);
        }

        #endregion

        #region Check Chunk Header

        /// <summary>
        /// Check if a chunk header is valid.
        /// </summary>
        /// <param name="chunkCompSize">The chunk compressed size.</param>
        /// <param name="chunkSize">The chunk original size.</param>
        /// <exception cref="Exception">The data was invalid.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckChunkHeader(int chunkCompSize, int chunkSize)
        {
            // If the chunk header specifies impossible values, throw an exception
            if (chunkCompSize < 0 || chunkSize < 0 || chunkCompSize > MaxChunkSize || chunkSize > MaxChunkSize)
            {
                throw new InvalidDataException("Compressed chunk header corrupted.");
            }
        }

        #endregion

        #region Decompress

        /// <summary>
        /// Decompresses the specified data to the specified destination.
        /// </summary>
        /// <param name="source">The data to decompress.</param>
        /// <param name="dest">The destination to decompress to.</param>
        /// <param name="bigEndian">Whether or not the data is big endian.</param>
        private void Decompress(ReadOnlySpan<byte> source, Span<byte> dest, bool bigEndian)
        {
            int size = dest.Length;
            int read = 0;

            // Setup reader and writer for convenience
            var br = new BinarySpanReader(source, bigEndian);
            var bw = new BinarySpanWriter(dest, bigEndian);

            // Keep reading chunks until we have all the required data
            while (read < size)
            {
                // Read the next chunk header
                int chunkCompSize = br.ReadInt32();
                int chunkSize = br.ReadInt32();
                CheckChunkHeader(chunkCompSize, chunkSize);

                // Read the compressed data and setup a buffer for the decompressed data
                var compBuf = br.ReadByteSpan(chunkCompSize);
                var decompBuf = dest.Slice(bw.Position, chunkSize);

                // Decompress the chunk and write it
                DecompressChunk(compBuf, decompBuf, chunkCompSize, chunkSize);
                bw.Position += chunkSize; // We modified the buffer the writer is using externally, so notify the writer position

                // Align by 4 relative to the start of all chunks
                br.AlignRelative(0, 4);

                // Increment how much we've read so far
                read += chunkSize;
            }
        }

        /// <summary>
        /// Decompresses the specified data to the specified destination.
        /// </summary>
        /// <param name="br">The compressed data reader.</param>
        /// <param name="bw">The decompressed data writer.</param>
        /// <param name="fileOffset">The offset of the entire file being decompressed.</param>
        /// <param name="size">The original file size.</param>
        private void Decompress(BinaryStreamReader br, BinaryStreamWriter bw, long fileOffset, long size)
        {
            long read = 0;

            // Rent a decompression buffer
            byte[] rawDecompBuf = ArrayPool<byte>.Shared.Rent(MaxChunkSize);

            // Keep reading chunks until we have all the required data
            while (read < size)
            {
                // Read the next chunk header
                int chunkCompSize = br.ReadInt32();
                int chunkSize = br.ReadInt32();
                CheckChunkHeader(chunkCompSize, chunkSize);

                // Read the compressed data and setup a buffer for the decompressed data
                ReadOnlySpan<byte> compBuf = br.ReadBytes(chunkCompSize);
                Span<byte> decompBuf = rawDecompBuf.AsSpan(0, chunkSize);

                // Decompress the chunk and write it
                DecompressChunk(compBuf, decompBuf, chunkCompSize, chunkSize);
                bw.WriteByteSpan(decompBuf);

                // Align by 4 relative to the start of all chunks
                br.AlignRelative(fileOffset, 4);

                // Increment how much we've read so far
                read += chunkSize;
            }

            // Make sure to return the rented decompression buffer
            ArrayPool<byte>.Shared.Return(rawDecompBuf);
        }

        #endregion

        #region Decompress Chunk

        /// <summary>
        /// Decompresses the specified chunk.
        /// </summary>
        /// <param name="source">The data to decompress.</param>
        /// <param name="dest">The destination to decompress to.</param>
        /// <param name="chunkCompSize">The chunk compressed size.</param>
        /// <param name="chunkSize">The chunk original size.</param>
        /// <exception cref="Exception">Decompression failed.</exception>
        /// <exception cref="NotSupportedException">The version was unknown.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DecompressChunk(ReadOnlySpan<byte> source, Span<byte> dest, int chunkCompSize, int chunkSize)
        {
            bool isCompressed = chunkCompSize < chunkSize;
            if (!isCompressed)
            {
                // If the chunk compression size is the same (possible), or greater than (shouldn't be) the chunk size, then it isn't compressed
                // Copy it over raw
                source.CopyTo(dest);
                return;
            }

            // If the chunk compression size is smaller than the original size, it must be compressed
            switch (Version)
            {
                case 3:
                    // Decompress with zlib and make sure it succeeded
                    int zlibAmountDecomp = Zlib.Decompress(source, dest);
                    if (zlibAmountDecomp != chunkSize)
                    {
                        throw new Exception("Chunked zlib decompression failure.");
                    }
                    break;
                case 4:
                    // Decompress with oodle and make sure it succeeded
#if DEBUG
                    const OodleLZ_Verbosity verbosity = OodleLZ_Verbosity.None; // Set this for debugging as necessary
#else
                    const OodleLZ_Verbosity verbosity = OodleLZ_Verbosity.None;
#endif

                    long oodleAmountDecomp = Oodle.GetOodleCompressor().Decompress(source, dest, OodleLZ_FuzzSafe.Yes, OodleLZ_CheckCRC.No, verbosity, OodleLZ_Decode_ThreadPhase.Unthreaded);
                    if (oodleAmountDecomp != chunkSize)
                    {
                        throw new Exception("Chunked oodle decompression failure.");
                    }
                    break;
                default:
                    throw new NotSupportedException($"Unknown file version for chunk decompression: {Version}");
            }
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Reader.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}

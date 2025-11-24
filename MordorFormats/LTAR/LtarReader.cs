using Edoke.IO;
using MordorFormats.Compression;
using OodleCoreSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

namespace MordorFormats
{
    public class LtarReader : IDisposable
    {
        private const int MaxChunkSize = 65536;
        private const int PathsOffset = 48;

        private readonly BinaryStreamReader Reader;
        private bool disposedValue;

        public bool BigEndian { get; set; }
        public int Version { get; set; }
        public int Unk14 { get; set; }
        public List<LtarFile> Files { get; set; }
        public List<LtarFolder> Folders { get; set; }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Is(BinaryStreamReader br)
        {
            string magic = br.GetASCII(0, 4);
            return magic == "LTAR" || magic == "RATL";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Is(string path)
        {
            using var br = new BinaryStreamReader(path);
            return Is(br);
        }

        #endregion

        #region IsRead

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static LtarReader Read(string path)
            => new LtarReader(new BinaryStreamReader(path));

        #endregion

        #region ReadFile

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

        public void ReadFile(LtarFile file, Stream output)
        {
            Reader.Seek(file.Offset);

            using var bw = new BinaryStreamWriter(output, BigEndian);
            Decompress(Reader, bw, file.Offset, file.Size);
        }

        #endregion

        #region Check Chunk Header

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckChunkHeader(int chunkCompSize, int chunkSize)
        {
            // If the chunk header specifies impossible values, throw an exception
            if ((chunkCompSize < 0 || chunkSize < 0) || (chunkCompSize > MaxChunkSize || chunkSize > MaxChunkSize))
            {
                throw new Exception("Compressed chunk header corrupted.");
            }
        }

        #endregion

        #region Decompress

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

        private void Decompress(BinaryStreamReader br, BinaryStreamWriter bw, long offset, long size)
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
                br.AlignRelative(offset, 4);

                // Increment how much we've read so far
                read += chunkSize;
            }

            // Make sure to return the rented decompression buffer
            ArrayPool<byte>.Shared.Return(rawDecompBuf);
        }

        #endregion

        #region Decompress Chunk

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
                    long oodleAmountDecomp = Oodle.GetOodleCompressor().Decompress(source, dest, OodleLZ_FuzzSafe.Yes, OodleLZ_CheckCRC.No, OodleLZ_Verbosity.None, OodleLZ_Decode_ThreadPhase.Unthreaded);
                    if (oodleAmountDecomp != chunkSize)
                    {
                        throw new Exception("Chunked oodle decompression failure.");
                    }
                    break;
                default:
                    throw new NotSupportedException($"Unknown file version: {Version}");
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

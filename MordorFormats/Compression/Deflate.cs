using System;
using System.IO;
using System.IO.Compression;

namespace MordorFormats.Compression
{
    /// <summary>
    /// A compression helper for deflate related functions.
    /// </summary>
    internal static class Deflate
    {
        /// <summary>
        /// Decompresses from the source to the destination.
        /// </summary>
        /// <param name="source">The source to decompress from.</param>
        /// <param name="destination">The destination to decompress to.</param>
        /// <returns>The amount decompressed.</returns>
        /// <exception cref="Exception">Couldn't read all data to decompress.</exception>
        public static unsafe int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            fixed (byte* pSource = &source[0])
            {
                using var inStream = new UnmanagedMemoryStream(pSource, source.Length);
                using var deflateStream = new DeflateStream(inStream, CompressionMode.Decompress);

                int remaining = destination.Length;
                int totalRead = 0;
                int read;
                do
                {
                    read = deflateStream.Read(destination.Slice(totalRead, remaining));
                    totalRead += read;
                    remaining -= read;

                    if (read == 0 && remaining > 0)
                    {
                        throw new Exception("Hit the end of deflate stream before getting all expected data.");
                    }
                }
                while (remaining > 0);

                return totalRead;
            }
        }

        /// <summary>
        /// Compresses from the source to the destination.
        /// </summary>
        /// <param name="source">The source to compress from.</param>
        /// <param name="destination">The destination to compress to.</param>
        /// <returns>The amount compressed.</returns>
        /// <exception cref="Exception">Couldn't compress all data.</exception>
        public static unsafe int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            fixed (byte* pSource = &source[0])
            {
                using var inStream = new UnmanagedMemoryStream(pSource, source.Length);
                using var deflateStream = new DeflateStream(inStream, CompressionMode.Compress);

                int remaining = destination.Length;
                int totalWritten = 0;
                int written;

                do
                {
                    long start = deflateStream.Position;
                    deflateStream.Write(destination);
                    long end = deflateStream.Position;
                    written = (int)(end - start);
                    totalWritten += written;
                    remaining -= written;

                    if (written == 0 && remaining > 0)
                    {
                        throw new Exception("Could not write all data to deflate stream.");
                    }
                }
                while (remaining > 0);

                return totalWritten;
            }
        }
    }
}

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
        public static unsafe int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            fixed (byte* pSource = &source[0])
            {
                using var inStream = new UnmanagedMemoryStream(pSource, source.Length);
                using var outStream = new MemoryStream(destination.Length);
                using var deflateStream = new DeflateStream(inStream, CompressionMode.Decompress);
                deflateStream.CopyTo(outStream);
                deflateStream.Flush();

                int written = (int)outStream.Length;
                outStream.Position = 0;
                outStream.ReadExactly(destination[..written]);
                return written;
            }
        }

        /// <summary>
        /// Compresses from the source to the destination.
        /// </summary>
        /// <param name="source">The source to compress from.</param>
        /// <param name="destination">The destination to compress to.</param>
        /// <returns>The amount compressed.</returns>
        public static unsafe int Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            fixed (byte* pSource = &source[0])
            {
                using var inStream = new UnmanagedMemoryStream(pSource, source.Length);
                using var outStream = new MemoryStream(destination.Length);
                using var deflateStream = new DeflateStream(outStream, CompressionMode.Compress);
                inStream.CopyTo(deflateStream);
                deflateStream.Flush();

                int written = (int)outStream.Length;
                outStream.Position = 0;
                outStream.ReadExactly(destination[..written]);
                return written;
            }
        }
    }
}

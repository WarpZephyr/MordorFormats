using System;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace MordorFormats.Compression
{
    /// <summary>
    /// A compression helper for zlib related functions.
    /// </summary>
    internal static class Zlib
    {
        /// <summary>
        /// The max value of the cmf byte.
        /// </summary>
        public const int CmfMax = 0x78;

        /// <summary>
        /// The max value of the flg byte.
        /// </summary>
        public const int FlgMax = 0xDA;

        /// <summary>
        /// Decompresses from the source to the destination.
        /// </summary>
        /// <param name="source">The source to decompress from.</param>
        /// <param name="destination">The destination to decompress to.</param>
        /// <returns>The amount decompressed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
            => Deflate.Decompress(source[2..], destination);

        /// <summary>
        /// Compresses from the source to the destination.
        /// </summary>
        /// <param name="source">The source to compress from.</param>
        /// <param name="destination">The destination to compress to.</param>
        /// <param name="level">The compression level to use.</param>
        /// <param name="cmf">The value of the cmf byte.</param>
        /// <param name="flg">The value of the flg byte.</param>
        /// <returns>The amount compressed.</returns>
        public static int Compress(ReadOnlySpan<byte> source, Span<byte> destination, CompressionLevel level, byte cmf, byte flg)
        {
            destination[0] = cmf;
            destination[1] = flg;
            return Deflate.Compress(source, destination[2..], level);
        }
    }
}

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace MordorFormats.Compression
{
    internal static class Zlib
    {
        public const int CmfMax = 0x78;
        public const int FlgMax = 0xDA;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Decompress(ReadOnlySpan<byte> source, Span<byte> destination)
            => Deflate.Decompress(source[2..], destination);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] Decompress(ReadOnlySpan<byte> source)
            => Deflate.Decompress(source[2..]);

        public static void Compress(ReadOnlySpan<byte> source, Span<byte> destination, byte cmf, byte flg)
        {
            destination[0] = cmf;
            destination[1] = flg;
            Deflate.Compress(source, destination[2..]);
        }

        public static byte[] Compress(ReadOnlySpan<byte> source, byte cmf, byte flg)
        {
            using var outStream = new MemoryStream();
            outStream.WriteByte(cmf);
            outStream.WriteByte(flg);
            Deflate.Compress(source, outStream);
            return outStream.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsZlib(ReadOnlySpan<byte> source)
        {
            // Check we can even read the source to do this check
            if (source.Length < 2)
                return false;

            byte cmf = source[0];
            byte flg = source[1];

            byte cinfo = (byte)(cmf >> 4);
            byte cm = (byte)(cmf & 0b00001111);
            return cm == 8 && cinfo < 8 && (((cmf * 256) + flg) % 31 == 0);
        }
    }
}

using System;
using System.IO;
using System.IO.Compression;

namespace MordorFormats.Compression
{
    internal static class Deflate
    {
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
                        throw new Exception("Hit end of deflate stream before getting all expected data.");
                    }
                }
                while (remaining > 0);

                return totalRead;
            }
        }

        public static unsafe byte[] Decompress(ReadOnlySpan<byte> source)
        {
            fixed (byte* pSource = &source[0])
            {
                using var inStream = new UnmanagedMemoryStream(pSource, source.Length);
                using var outStream = new MemoryStream();
                using var deflateStream = new DeflateStream(inStream, CompressionMode.Decompress);
                deflateStream.CopyTo(outStream);
                return outStream.ToArray();
            }
        }

        public static unsafe void Compress(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            fixed (byte* pSource = &source[0])
            {
                using var inStream = new UnmanagedMemoryStream(pSource, source.Length);
                using var deflateStream = new DeflateStream(inStream, CompressionMode.Compress);
                deflateStream.Write(destination);
            }
        }

        public static unsafe byte[] Compress(ReadOnlySpan<byte> source)
        {
            fixed (byte* pSource = &source[0])
            {
                using var inStream = new UnmanagedMemoryStream(pSource, source.Length);
                using var outStream = new MemoryStream();
                using var deflateStream = new DeflateStream(inStream, CompressionMode.Compress);
                deflateStream.CopyTo(outStream);
                return outStream.ToArray();
            }
        }

        internal static unsafe void Compress(ReadOnlySpan<byte> source, Stream outStream)
        {
            fixed (byte* pSource = &source[0])
            {
                using var inStream = new UnmanagedMemoryStream(pSource, source.Length);
                using var deflateStream = new DeflateStream(inStream, CompressionMode.Compress);
                deflateStream.CopyTo(outStream);
            }
        }
    }
}

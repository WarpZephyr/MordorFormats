using Edoke.IO;

namespace MordorFormats.LTAR
{
    /// <summary>
    /// A file entry within a lithtech archive.
    /// </summary>
    public class LtarFile
    {
        /// <summary>
        /// The offset paths always begin at.
        /// </summary>
        private const int PathsOffset = 48;

        /// <summary>
        /// The offset the file is at, maintained to know where to read from.
        /// </summary>
        internal long Offset;

        /// <summary>
        /// The compressed size of the file, maintained to know how big the compressed data is.
        /// </summary>
        internal long CompressedSize;

        /// <summary>
        /// The original size of the file, maintained to know how big the original data is.
        /// </summary>
        internal long Size;

        /// <summary>
        /// The name of the file, does not include any folder information.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The flags of this file, unknown purpose, always seems to be 9.
        /// </summary>
        public int Flags { get; set; }

        /// <summary>
        /// Creates a new <see cref="LtarFile"/>.
        /// </summary>
        /// <param name="name">The name of the file, does not include any folder information.</param>
        /// <param name="flags">The flags of this file, unknown purpose, always seems to be 9.</param>
        public LtarFile(string name, int flags = 9)
        {
            Offset = -1;
            CompressedSize = -1;
            Size = -1;
            Name = name;
            Flags = flags;
        }

        /// <summary>
        /// Reads an <see cref="LtarFile"/> from a stream.
        /// </summary>
        /// <param name="br">The stream reader.</param>
        /// <param name="version">The version of the lithtech archive.</param>
        internal LtarFile(BinaryStreamReader br, int version)
        {
            int nameOffset = br.ReadInt32();
            Offset = br.ReadInt64();
            CompressedSize = br.ReadInt64();
            Size = br.ReadInt64();

            if (version >= 4)
            {
                br.AssertByte(1);
                Flags = br.ReadByte(); // Always seems to be 9? Compression Level?
            }
            else
            {
                Flags = br.ReadInt32(); // Always seems to be 9? Compression Level?
            }

            Name = br.GetUTF8(PathsOffset + nameOffset);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}

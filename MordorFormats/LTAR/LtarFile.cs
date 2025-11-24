using Edoke.IO;

namespace MordorFormats
{
    public class LtarFile
    {
        private const int PathsOffset = 48;

        internal long Offset;
        internal long CompressedSize;
        internal long Size;

        public string Name { get; set; }
        public int Flags { get; set; }

        public LtarFile(string name, byte flags)
        {
            Offset = -1;
            CompressedSize = -1;
            Size = -1;
            Name = name;
            Flags = flags;
        }

        public LtarFile(string name) : this(name, 9) { }

        internal LtarFile(BinaryStreamReader br, int version)
        {
            int nameOffset = br.ReadInt32();
            Offset = br.ReadInt64();
            CompressedSize = br.ReadInt64();
            Size = br.ReadInt64();

            if (version >= 4)
            {
                br.AssertByte(1);
                Flags = br.ReadByte();
            }
            else
            {
                Flags = br.ReadInt32();
            }

            Name = br.GetUTF8(PathsOffset + nameOffset);
        }
    }
}

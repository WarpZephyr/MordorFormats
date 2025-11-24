using Edoke.IO;


namespace MordorFormats
{
    public class LtarFolder
    {
        private const int PathsOffset = 48;

        public string Name { get; set; }
        public int ChildIndex { get; set; }
        public int NextSiblingIndex { get; set; }
        public int FileCount { get; set; }

        public LtarFolder(string name)
        {
            Name = name;
            ChildIndex = -1;
            NextSiblingIndex = -1;
            FileCount = 0;
        }

        internal LtarFolder(BinaryStreamReader br)
        {
            int nameOffset = br.ReadInt32();
            ChildIndex = br.ReadInt32();
            NextSiblingIndex = br.ReadInt32();
            FileCount = br.ReadInt32();

            Name = br.GetUTF8(PathsOffset + nameOffset);
        }
    }
}

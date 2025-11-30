using Edoke.IO;

namespace MordorFormats.LTAR
{
    /// <summary>
    /// A folder entry within a lithtech archive.
    /// </summary>
    public class LtarFolder
    {
        /// <summary>
        /// The offset paths always begin at.
        /// </summary>
        private const int PathsOffset = 48;

        /// <summary>
        /// The full folder path of the entry.<br/>
        /// Uses \ directory separators.<br/>
        /// Does not have leading or trailing directory separators.<br/>
        /// The root folder is always an empty string.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The index of the first child folder of this folder.
        /// </summary>
        public int ChildIndex { get; set; }

        /// <summary>
        /// The index of the next sibling folder of this folder.
        /// </summary>
        public int NextSiblingIndex { get; set; }

        /// <summary>
        /// The number of files this folder takes from the list, starting from where all previous folders took theirs.
        /// </summary>
        public int FileCount { get; set; }

        /// <summary>
        /// Creates a new <see cref="LtarFolder"/>.
        /// </summary>
        /// <param name="name"></param>
        public LtarFolder(string name)
        {
            Name = name;
            ChildIndex = -1;
            NextSiblingIndex = -1;
            FileCount = 0;
        }

        /// <summary>
        /// Reads an <see cref="LtarFolder"/> from a stream.
        /// </summary>
        /// <param name="br">The stream reader.</param>
        internal LtarFolder(BinaryStreamReader br)
        {
            int nameOffset = br.ReadInt32();
            ChildIndex = br.ReadInt32();
            NextSiblingIndex = br.ReadInt32();
            FileCount = br.ReadInt32();

            Name = br.GetUTF8(PathsOffset + nameOffset);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}

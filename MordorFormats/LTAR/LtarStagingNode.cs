using Edoke.IO;
using MordorFormats.Utilities;
using System;
using System.Collections.Generic;

namespace MordorFormats.LTAR
{
    /// <summary>
    /// A node representing a single folder and it's children.<br/>
    /// Used for staging in writing Lithtech archives.
    /// </summary>
    public class LtarStagingNode
    {
        /// <summary>
        /// The full folder path of the node.<br/>
        /// Uses \ directory separators.<br/>
        /// Does not have leading or trailing directory separators.<br/>
        /// The root node is always an empty string.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The files under this node.
        /// </summary>
        public Dictionary<string, LtarStagingFile> Files { get; set; }

        /// <summary>
        /// The folders under this node.
        /// </summary>
        public Dictionary<string, LtarStagingNode> Children { get; set; }

        /// <summary>
        /// Creates a new <see cref="LtarStagingNode"/>.
        /// </summary>
        /// <param name="name"></param>
        public LtarStagingNode(string name)
        {
            Name = name;
            Files = [];
            Children = [];
        }

        /// <summary>
        /// Writes the folder entry of this node.
        /// </summary>
        /// <param name="bw">The stream writer.</param>
        /// <param name="nameOffset">The offset to the node's folder name</param>
        /// <param name="index">The index of this node.</param>
        internal void WriteFolder(BinaryStreamWriter bw, int nameOffset, int index)
        {
            bw.WriteInt32(nameOffset);
            bw.WriteInt32(Children.Count > 0 ? index + 1 : -1);
            bw.ReserveInt32($"NextSiblingIndex_{index}");
            bw.WriteInt32(Files.Count);
        }

        /// <summary>
        /// Gets the total number of files contained in this node.
        /// </summary>
        /// <returns>The total number of files contained in this node.</returns>
        public int GetFileCount()
        {
            int fileCount = Files.Count;
            foreach (var node in Children.Values)
                fileCount += node.GetFileCount();
            return fileCount;
        }

        /// <summary>
        /// Gets the total number of folders contained in this node, including the node itself.
        /// </summary>
        /// <returns>The total number of folders contained in this node, including the node itself.</returns>
        public int GetFolderCount()
        {
            int folderCount = 1;
            foreach (var node in Children.Values)
                folderCount += node.GetFolderCount();
            return folderCount;
        }

        /// <summary>
        /// Mounts a file into the node hierarchy.
        /// </summary>
        /// <param name="rootFolder">The root folder path to strip from the file path.</param>
        /// <param name="filePath">The full file path.</param>
        /// <param name="flags">The flags of this file, unknown purpose, always seems to be 9.</param>
        public void MountFile(string rootFolder, string filePath, int flags = 9)
        {
            string path = PathHelper.NormalizeDirectorySeparators(filePath, '\\');
            string root = PathHelper.NormalizeDirectorySeparators(rootFolder, '\\');

            path = path.Replace(root, string.Empty);
            path = PathHelper.RemoveRoot(path);

            var pathSpan = path.AsSpan();
            int partCount = PathHelper.GetPathPartCount(pathSpan);
            Span<Range> partRanges = new Range[partCount];

            partCount = pathSpan.Split(partRanges, '\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            partRanges = partRanges[..partCount];
            InsertFile(path, partRanges, new LtarStagingFile(filePath, flags));
        }

        /// <summary>
        /// Inserts a file into the node hierarchy.
        /// </summary>
        /// <param name="path">The full file path.</param>
        /// <param name="parts">The directory separated parts of the file path without it's root folder path.</param>
        /// <param name="file">The file entry to insert.</param>
        private void InsertFile(ReadOnlySpan<char> path, ReadOnlySpan<Range> parts, LtarStagingFile file)
        {
            if (parts.Length < 1)
            {
                // Shouldn't happen ideally
                return;
            }

            if (parts.Length == 1)
            {
                string name = new string(path[parts[0]]);
                file.Name = name;

                if (!Files.TryAdd(name, file))
                {
                    // Overwrite
                    Files[name] = file;
                }

                return;
            }

            string nextPart = $"{Name}\\{path[parts[0]]}";
            if (nextPart.StartsWith('\\'))
            {
                nextPart = nextPart[1..];
            }

            if (!Children.TryGetValue(nextPart, out LtarStagingNode? nextNode))
            {
                nextNode = new LtarStagingNode(nextPart);
                Children.Add(nextPart, nextNode);
            }

            nextNode.InsertFile(path, parts[1..], file);
        }

        /// <summary>
        /// Enumerate only the files contained within this node.
        /// </summary>
        /// <returns>An enumerable of only the files contained within this node.</returns>
        public IEnumerable<LtarStagingFile> EnumerateFiles()
        {
            foreach (var file in Files)
            {
                yield return file.Value;
            }
        }

        /// <summary>
        /// Enumerate only the folders contained within this node.
        /// </summary>
        /// <returns>An enumerable of only the folders contained within this node.</returns>
        public IEnumerable<LtarStagingNode> EnumerateFolders()
        {
            foreach (var child in Children)
            {
                yield return child.Value;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}

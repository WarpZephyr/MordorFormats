using Edoke.IO;
using MordorFormats.Utilities;
using OodleCoreSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace MordorFormats.LTAR
{
    /// <summary>
    /// A writer for Lithtech archives.
    /// </summary>
    public class LtarWriter
    {
        /// <summary>
        /// The offset paths always begin at.
        /// </summary>
        private const int PathsOffset = 48;

        /// <summary>
        /// Whether or not to write in big endian.
        /// </summary>
        public bool BigEndian { get; set; }

        // I'm unsure I should provide direct enum access because the underlying API could change
        // System.Compression.IO's API seems dumbed down for deflate compression level
        /// <summary>
        /// Whether or not to prefer smallest compression for zlib in versions supporting it.
        /// </summary>
        public bool UseMaxZlibCompressionLevel { get; set; }

        /// <summary>
        /// The oodle compressor to use in versions supporting it.
        /// </summary>
        public OodleLZ_Compressor OodleCompressor { get; set; }

        /// <summary>
        /// The oodle compression level to use in versions supporting it.
        /// </summary>
        public OodleLZ_CompressionLevel OodleCompressionLevel { get; set; }

        /// <summary>
        /// The version to use.<br/>
        /// Notes for 3:<br/>
        /// - Used in Middle Earth: Shadow of Mordor<br/>
        /// - File extension: .arch05<br/>
        /// - Compression is max zlib deflate.<br/>
        /// - Paths are aligned to 4 bytes.<br/>
        /// - "Flags" in file entries are a 4 byte int.<br/>
        /// <br/>
        /// Notes for 4:<br/>
        /// - Used in Middle Earth: Shadow of War<br/>
        /// - File extension: .arch06<br/>
        /// - Compression uses oodle 5 of unknown settings.<br/>
        /// - Paths are not aligned.<br/>
        /// - There is an unknown byte of value 1 in file entries just above "Flags".<br/>
        /// - "Flags" in file entries are 1 byte.<br/>
        /// - There is always (6 * fileCount) padding bytes before data.<br/>
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Unknown; Always 1.
        /// </summary>
        public int Unk14 { get; set; }

        /// <summary>
        /// The root node in a file tree for the archive to be written.
        /// </summary>
        public LtarStagingNode RootNode { get; private set; }

        /// <summary>
        /// Create a new <see cref="LtarWriter"/>.
        /// </summary>
        public LtarWriter()
        {
            BigEndian = false;
            Version = 4;
            Unk14 = 1;
            OodleCompressor = OodleLZ_Compressor.Mermaid;
            OodleCompressionLevel = OodleLZ_CompressionLevel.HyperFast4;
            RootNode = new LtarStagingNode(string.Empty);
        }

        /// <summary>
        /// Write to the specified file path.
        /// </summary>
        /// <param name="path">The file path to write to.</param>
        public void Write(string path)
        {
            using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            using var bw = new BinaryStreamWriter(fs, BigEndian, false);
            Write(bw);
        }

        /// <summary>
        /// Write to the specified stream starting at position 0.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="leaveOpen">Whether or not to leave the stream open after writing has finished.</param>
        /// <exception cref="Exception">The stream was not at position 0.</exception>
        public void Write(Stream stream, bool leaveOpen = false)
        {
            if (stream.Position != 0)
            {
                throw new Exception("Streams to be written to should start at 0."); // Just in case, for now
            }

            using var bw = new BinaryStreamWriter(stream, BigEndian, leaveOpen);
            Write(bw);
        }

        /// <summary>
        /// Write using the specified stream writer.
        /// </summary>
        /// <param name="bw">The stream writer.</param>
        internal void Write(BinaryStreamWriter bw)
        {
            // Setup variables
            int folderCount = RootNode.GetFolderCount();
            int fileCount = RootNode.GetFileCount();

            // We need this to do name de-duplication, as the original data has done
            // Sometimes two separate folders contain a file of the same name,
            // So it's possible two file entries will use the same name
            var nameOffsetMapping = new Dictionary<string, int>(folderCount + fileCount);

            int fileIndex = 0;
            int folderIndex = 0;
            bw.BigEndian = BigEndian;

            // Write header
            if (BigEndian)
                bw.WriteASCII("RATL");
            else
                bw.WriteASCII("LTAR");

            bw.WriteInt32(Version);
            bw.ReserveInt32("PathsSize");
            bw.WriteInt32(folderCount);
            bw.WriteInt32(fileCount);
            bw.WriteInt32(Unk14);
            bw.WritePattern(24, 0);

            // Write names
            void WriteName(string name)
            {
                // Try to add the name to the name offset mapping,
                // If it succeeds write the name as it's unique
                if (nameOffsetMapping.TryAdd(name, (int)bw.Position - PathsOffset))
                {
                    // Correct name for incorrect usages of the API
                    name = PathHelper.NormalizeDirectorySeparators(name, '\\');
                    name = name.Trim('\\');

                    // Then write it
                    bw.WriteASCII(name, true);
                    if (Version == 3)
                    {
                        bw.Pad(4, 0); // Pad for version 3
                    }
                }
            }

            void WriteNames(LtarStagingNode node)
            {
                WriteName(node.Name);
                foreach (var file in node.EnumerateFiles())
                {
                    WriteName(file.Name);
                }

                foreach (var folder in node.EnumerateFolders())
                {
                    WriteNames(folder);
                }
            }

            long pathsStart = bw.Position;
            WriteNames(RootNode);
            bw.FillInt32("PathsSize", (int)(bw.Position - pathsStart));

            // Write files
            void WriteFiles(LtarStagingNode node)
            {
                foreach (var file in node.EnumerateFiles())
                {
                    file.Write(bw, Version, nameOffsetMapping[file.Name], fileIndex);
                    fileIndex++;
                }

                foreach (var folder in node.EnumerateFolders())
                {
                    WriteFiles(folder);
                }
            }

            fileIndex = 0; // Reset before we begin
            WriteFiles(RootNode);

            // Write folders
            void WriteFolders(LtarStagingNode node, bool lastChild)
            {
                int currentIndex = folderIndex; // Save the current index on the stack so we can fill the next sibling later.
                node.WriteFolder(bw, nameOffsetMapping[node.Name], folderIndex);
                folderIndex++; // Increment for the next folder

                // Keep track of how many children are remaining, so we can tell them if they are the last child, or have another sibling
                int remainingChildren = node.Children.Count;
                foreach (var child in node.Children.Values)
                {
                    WriteFolders(child, remainingChildren == 1);
                    remainingChildren--;
                }

                if (lastChild)
                {
                    // If we are the last child, set the next sibling index to -1
                    bw.FillInt32($"NextSiblingIndex_{currentIndex}", -1);
                }
                else
                {
                    // If we aren't the last child, the folder index has already been incremented to the next sibling index,
                    // Thanks to the ordered and recursive way we are writing the children
                    bw.FillInt32($"NextSiblingIndex_{currentIndex}", folderIndex);
                }
            }

            folderIndex = 0; // Reset before we begin
            WriteFolders(RootNode, true);

            // Write padding between headers and data for newer version
            if (Version >= 4)
            {
                bw.WritePattern(fileCount * 6, 0); // Why do they do this?
            }

            // Write File Data
            void WriteFileData(LtarStagingNode node)
            {
                foreach (var file in node.EnumerateFiles())
                {
                    file.WriteData(bw, Version, fileIndex, UseMaxZlibCompressionLevel, OodleCompressor, OodleCompressionLevel);
                    fileIndex++;
                }

                foreach (var folder in node.EnumerateFolders())
                {
                    WriteFileData(folder);
                }
            }

            fileIndex = 0; // Reset before we begin
            WriteFileData(RootNode);
        }
    }
}

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace MordorFormats.Utilities
{
    /// <summary>
    /// A helper containing path related functions.
    /// </summary>
    internal static class PathHelper
    {
        /// <summary>
        /// Removes root from the specified path.
        /// </summary>
        /// <param name="path">The path to remove root from.</param>
        /// <returns>A new path with root removed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string RemoveRoot(string path)
            => path[Path.GetPathRoot(path.AsSpan()).Length..];

        /// <summary>
        /// Normalizes the directory separators of the specified path to the specified separator.
        /// </summary>
        /// <param name="path">The path to normalize.</param>
        /// <param name="separator">The separator to normalize to.</param>
        /// <returns>A new path with directory separators normalized.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string NormalizeDirectorySeparators(string path, char separator)
            => path.Replace('/', separator).Replace(Path.DirectorySeparatorChar, separator).Replace(Path.AltDirectorySeparatorChar, separator);

        /// <summary>
        /// Gets the number of directory separated parts in a path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The number of directory separated parts in a path.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetPathPartCount(ReadOnlySpan<char> path)
        {
            if (path.IsEmpty)
                return 0;

            int count = 1;
            for (int i = 0; i < path.Length; i++)
            {
                if (path[i] == '/' ||
                    path[i] == '\\' ||
                    path[i] == Path.DirectorySeparatorChar ||
                    path[i] == Path.AltDirectorySeparatorChar)
                {
                    count++;
                }
            }

            return count;
        }
    }
}

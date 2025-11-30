using System.Runtime.CompilerServices;

namespace MordorFormats.Utilities
{
    /// <summary>
    /// A helper containing math related functions.
    /// </summary>
    internal static class MathHelper
    {
        /// <summary>
        /// Aligns numbers to binary only alignments.
        /// </summary>
        /// <param name="num">The number to align.</param>
        /// <param name="alignment">The binary alignment to align by.</param>
        /// <returns>The aligned number.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int BinaryAlign(int num, int alignment)
            => num + --alignment & ~alignment;

        /// <summary>
        /// Aligns numbers to binary only alignments.
        /// </summary>
        /// <param name="num">The number to align.</param>
        /// <param name="alignment">The binary alignment to align by.</param>
        /// <returns>The aligned number.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long BinaryAlign(long num, long alignment)
            => num + --alignment & ~alignment;
    }
}

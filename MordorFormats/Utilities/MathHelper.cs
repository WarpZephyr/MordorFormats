using System.Runtime.CompilerServices;

namespace MordorFormats.Utilities
{
    internal static class MathHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int BinaryAlign(int num, int alignment)
            => num + --alignment & ~alignment;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long BinaryAlign(long num, long alignment)
            => num + --alignment & ~alignment;
    }
}

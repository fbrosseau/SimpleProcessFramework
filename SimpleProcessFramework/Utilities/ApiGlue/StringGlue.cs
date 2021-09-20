using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace System
{
    internal static class StringGlue
    {
#if NETCOREAPP3_1_PLUS
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(string str, char c)
        {
            return str.Contains(c);
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this string str, char c)
        {
            return str.AsSpan().IndexOf(c) != -1;
        }
#endif

#if NET6_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string[] Split(string str, char separator, StringSplitOptions options) => str.Split(separator, options);
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string[] Split(this string str, char separator, StringSplitOptions options)
        {
            return str.Split(new[] { separator }, options);
        }
#endif
    }
}

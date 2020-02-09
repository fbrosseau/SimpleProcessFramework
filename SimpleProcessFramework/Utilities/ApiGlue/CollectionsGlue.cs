using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Spfx.Utilities.ApiGlue
{
    internal static class CollectionsGlue
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        public static void EnsureCapacity<TKey,TValue>(this Dictionary<TKey, TValue> dict, int cap)
        {
            // oh well!
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureCapacity<TKey, TValue>(Dictionary<TKey, TValue> dict, int cap)
        {
            dict.EnsureCapacity(cap);
        }
#endif

#if NETFRAMEWORK || NETSTANDARD2_0
        public static void EnsureCapacity<TKey>(this HashSet<TKey> hashset, int cap)
        {
            // oh well!
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureCapacity<TKey>(HashSet<TKey> hashset, int cap)
        {
            hashset.EnsureCapacity(cap);
        }
#endif
    }
}

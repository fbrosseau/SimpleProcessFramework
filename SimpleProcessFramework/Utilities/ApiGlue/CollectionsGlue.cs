using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace System.Collections.Generic
{
    internal static class CollectionsGlue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NETFRAMEWORK && !NETSTANDARD2_0
        public static bool Remove<TKey, TValue>(Dictionary<TKey, TValue> dict, TKey key, out TValue val)
        {
            return dict.Remove(key, out val);
        }
#else
        public static bool Remove<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, out TValue val)
        {
            if (dict.TryGetValue(key, out val))
            {
                dict.Remove(key);
                return true;
            }

            return false;
        }
#endif

#if NETFRAMEWORK || NETSTANDARD2_0
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

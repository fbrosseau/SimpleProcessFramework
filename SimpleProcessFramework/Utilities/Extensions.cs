using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Spfx.Utilities
{
    internal static class Extensions
    {
        public static IEnumerable<(T1 a, T2 b)> Zip<T1, T2>(
#if !NETCOREAPP3_0_PLUS
            this
#endif
            IEnumerable<T1> items1, IEnumerable<T2> items2)
        {
            return items1.Zip(items2, (a, b) => (a, b));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Rethrow(this Exception ex)
        {
            ExceptionDispatchInfo.Capture(ex).Throw();
        }

        public static IEnumerable<List<T>> GenerateCombinations<T>(this IReadOnlyList<T> list, int minLen = 1, int maxLen = int.MaxValue)
        {
            Guard.ArgumentNotNull(list, nameof(list));
            return GenerateCombinations(list, minLen, maxLen, 0, new List<T>());
        }

        private static IEnumerable<List<T>> GenerateCombinations<T>(IReadOnlyList<T> list, int minLen, int maxLen, int startOffset, List<T> currentList)
        {
            if (currentList.Count >= maxLen)
                yield break;

            for (int i = startOffset; i < list.Count; ++i)
            {
                var newList = new List<T>(currentList.Count + 1);
                newList.AddRange(currentList);
                newList.Add(list[i]);
                if (newList.Count >= minLen)
                    yield return newList;

                foreach (var sublist in GenerateCombinations(list, minLen, maxLen, i + 1, newList))
                    yield return sublist;
            }
        }
    }
}

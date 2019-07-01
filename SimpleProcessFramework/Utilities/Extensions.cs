using System.Collections.Generic;
using System.Linq;

namespace Spfx.Utilities
{
    internal static class Extensions
    {
        public static IEnumerable<(T1 a, T2 b)> Zip<T1, T2>(this IEnumerable<T1> items1, IEnumerable<T2> items2)
        {
            return items1.Zip(items2, (a, b) => (a, b));
        }
    }
}

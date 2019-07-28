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
#if !NETCOREAPP3_0
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
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace System.Threading
{
    internal static class AsyncDisposablesGlue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETFRAMEWORK || !NETSTANDARD2_1_PLUS
        public static ValueTask DisposeAsync(this Timer timer)
        {
            timer.Dispose();
            return default;
        }
#else
        public static ValueTask DisposeAsync(Timer timer)
        {
            return timer.DisposeAsync();
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETFRAMEWORK || !NETSTANDARD2_1_PLUS
        public static ValueTask DisposeAsync(this CancellationTokenRegistration ctr)
        {
            ctr.Dispose();
            return default;
        }
#else
        public static ValueTask DisposeAsync(CancellationTokenRegistration ctr)
        {
            return ctr.DisposeAsync();
        }
#endif
    }
}

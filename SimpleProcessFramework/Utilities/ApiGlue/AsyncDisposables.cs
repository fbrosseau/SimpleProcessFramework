using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace System.Threading
{
    internal static class AsyncDisposablesGlue
    {
#if NETFRAMEWORK || !NETSTANDARD2_1_PLUS
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask DisposeAsync(this Timer timer)
        {
            timer.Dispose();
            return default;
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask DisposeAsync(Timer timer)
        {
            return timer.DisposeAsync();
        }
#endif

#if NETFRAMEWORK || !NETSTANDARD2_1_PLUS
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask DisposeAsync(this CancellationTokenRegistration ctr)
        {
            ctr.Dispose();
            return default;
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask DisposeAsync(CancellationTokenRegistration ctr)
        {
            return ctr.DisposeAsync();
        }
#endif
    }
}

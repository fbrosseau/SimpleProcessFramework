using System.Net.Security;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO
{
    internal static class StreamGlue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETFRAMEWORK || !NETSTANDARD2_1_PLUS
        public static ValueTask DisposeAsync(this Stream s)
        {
            s.Dispose();
            return default;
        }
#else
        public static ValueTask DisposeAsync(Stream s)
        {
            return s.DisposeAsync();
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NETFRAMEWORK || !NETSTANDARD2_1_PLUS
        public static Task CopyToAsync(this Stream s, Stream other, CancellationToken ct)
        {
            return s.CopyToAsync(other);
        }
#else
        public static Task CopyToAsync(Stream s, Stream other, CancellationToken ct)
        {
            return s.CopyToAsync(other, ct);
        }
#endif

#if NETSTANDARD2_0
        private static Func<SslStream, Task> s_shutdownMethod;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task ShutdownAsync(this SslStream s)
        {
            // if we're running net48 or any netcore, this method will exist at the runtime level.
            // it's just that netstandard didn't include it.
            static Func<SslStream, Task> FindShutdownMethod()
            {
                var m = typeof(SslStream).GetMethod("ShutdownAsync", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (m is null)
                    return s => Task.CompletedTask; // oh well??

                return (Func<SslStream, Task>)Delegate.CreateDelegate(typeof(Func<SslStream, Task>), m);
            }

            if (s_shutdownMethod is null)
                s_shutdownMethod = FindShutdownMethod();

            return s_shutdownMethod?.Invoke(s);
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task ShutdownAsync(SslStream s)
        {
            return s.ShutdownAsync();
        }
#endif
    }
}

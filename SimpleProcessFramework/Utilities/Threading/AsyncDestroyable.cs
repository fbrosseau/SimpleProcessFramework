using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
    public interface IAsyncDestroyable : IDisposable
    {
        ValueTask TeardownAsync(CancellationToken ct = default);
    }

    public static class AsyncDestroyableExtensions
    {
        public static async Task TeardownAsync(this IAsyncDestroyable d, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            await d.TeardownAsync(cts.Token);
        }
    }

    public class AsyncDestroyable : Disposable, IAsyncDestroyable
#if NETCOREAPP3_0_PLUS || NETSTANDARD2_1_PLUS
        , IAsyncDisposable
#endif
    {
        public bool HasAsyncTeardownStarted { get; private set; }

        public async ValueTask TeardownAsync(CancellationToken ct = default)
        {
            using (this)
            {
                if (HasDisposeStarted || ct.IsCancellationRequested)
                    return;

                lock (m_disposeLock)
                {
                    if (HasDisposeStarted || HasAsyncTeardownStarted)
                        return;
                    HasAsyncTeardownStarted = true;
                }

                await OnTeardownAsync(ct).ConfigureAwait(false);
            }
        }

        protected override void ThrowIfDisposing()
        {
            base.ThrowIfDisposing();
            if (HasAsyncTeardownStarted)
                throw new ObjectDisposedException(GetType().FullName);
        }

        protected virtual ValueTask OnTeardownAsync(CancellationToken ct)
        {
            return default;
        }

        public ValueTask DisposeAsync()
        {
            return TeardownAsync();
        }
    }
}
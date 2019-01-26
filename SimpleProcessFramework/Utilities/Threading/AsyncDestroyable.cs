using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Utilities.Threading
{
    public interface IAsyncDestroyable : IDisposable
    {
        Task TeardownAsync(CancellationToken ct = default);
    }

    public static class AsyncDestroyableExtensions
    {
        public static async Task TeardownAsync(this IAsyncDestroyable d, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource(timeout))
            {
                await d.TeardownAsync(cts.Token);
            }
        }
    }

    public class AsyncDestroyable : Disposable, IAsyncDestroyable
    {
        public bool HasAsyncTeardownStarted { get; private set; }

        public Task TeardownAsync(CancellationToken ct = default)
        {
            if (HasDisposeStarted)
            {
                return Task.CompletedTask;
            }

            if (ct.IsCancellationRequested)
            {
                Dispose();
                return Task.CompletedTask;
            }

            lock (m_disposeLock)
            {
                if (HasDisposeStarted || HasAsyncTeardownStarted)
                    return Task.CompletedTask;
                HasAsyncTeardownStarted = true;
            }

            try
            {
                var t = OnTeardownAsync(ct);
                return t.ContinueWith((innerT, innerThis) =>
                {
                    ((IDisposable)innerThis).Dispose();
                }, this, ct);
            }
            catch
            {
                Dispose();
                return Task.CompletedTask;
            }
        }

        protected override void ThrowIfDisposing()
        {
            base.ThrowIfDisposing();
            if (HasAsyncTeardownStarted)
                throw new ObjectDisposedException(GetType().FullName);
        }

        protected virtual Task OnTeardownAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
    internal static partial class TaskEx
    {
        public static Task WaitAsync(this WaitHandle h) => WaitAsync(h, Timeout.InfiniteTimeSpan);
        public static Task<bool> WaitAsync(WaitHandle h, CancellationToken ct) => WaitAsync(h, Timeout.InfiniteTimeSpan, ct);
        public static Task<bool> WaitAsync(WaitHandle h, TimeSpan timeout) => WaitAsync(h, timeout, CancellationToken.None);

        private static Task<bool> WaitAsync(WaitHandle h, TimeSpan timeout, CancellationToken ct)
        {
            return new WaitHandleWaitAsyncState(h, timeout, ct).Task;
        }

        private class WaitHandleWaitAsyncState : TaskCompletionSource<bool>
        {
            private readonly WaitHandle m_handle;
            private readonly RegisteredWaitHandle m_registration;
            private readonly IDisposable m_ctRegistration;
            private static readonly Action<object> s_onCanceled = obj => ((WaitHandleWaitAsyncState)obj).OnCanceled();

            public WaitHandleWaitAsyncState(WaitHandle h, TimeSpan timeout, CancellationToken ct)
            {
                m_handle = h;

                m_registration = ThreadPool.UnsafeRegisterWaitForSingleObject(h, (object s, bool timedOut) =>
                {
                    var innerState = (WaitHandleWaitAsyncState)s;
                    innerState.OnCallback(timedOut);
                }, this, (int)timeout.TotalMilliseconds, true);

                if (ct.CanBeCanceled)
                {
                    m_ctRegistration = ct.Register(s_onCanceled, this, false);
                    if (Task.IsCompleted)
                        m_ctRegistration.Dispose();
                }
            }

            private void OnCanceled()
            {
                OnCallback(true);
            }

            private void OnCallback(bool timedOut)
            {
                if (TrySetResult(!timedOut))
                    OnCompleted();
            }

            private void OnCompleted()
            {
                m_registration.Unregister(m_handle);
                m_ctRegistration?.Dispose();
            }
        }
    }
}
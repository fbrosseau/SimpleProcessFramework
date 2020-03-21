using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
    internal abstract class BaseAsyncEvent : Disposable
    {
        protected EventWaiter m_firstWaiter;
        protected readonly object m_lock = new object();

        protected readonly struct WaitSyncOrAsyncResult
        {
            private readonly bool SyncResult;
            private readonly Task<bool> AsyncTask;

            public WaitSyncOrAsyncResult(bool syncResult)
            {
                AsyncTask = default;
                SyncResult = syncResult;
            }

            public WaitSyncOrAsyncResult(Task<bool> asyncTask)
            {
                SyncResult = default;
                AsyncTask = asyncTask;
            }

            public ValueTask AsVoidValueTask()
            {
                if (AsyncTask is null)
                    return default;
                return new ValueTask(AsyncTask);
            }

            public ValueTask<bool> AsValueTask()
            {
                if (AsyncTask is null)
                    return new ValueTask<bool>(SyncResult);
                return new ValueTask<bool>(AsyncTask);
            }
        }

        protected class EventWaiter : TaskCompletionSource<bool>, IDisposable
        {
            public EventWaiter Next;
            public EventWaiter Previous;
            private CancellationTokenRegistration m_ctRegistration;
            private Timer m_timer;
            private readonly BaseAsyncEvent m_owner;

            private static readonly TimerCallback s_rawTimeoutRequest = RawOnCancelRequested;
            private static readonly Action<object> s_rawCancelRequest = RawOnCancelRequested;

            public EventWaiter(BaseAsyncEvent e)
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
                m_owner = e;
            }

            internal void Activate(CancellationToken ct, TimeSpan timeout)
            {
                m_ctRegistration = ct.Register(s_rawCancelRequest, this, useSynchronizationContext: false);

                if (timeout != Timeout.InfiniteTimeSpan)
                {
                    m_timer = new Timer(s_rawTimeoutRequest, this, timeout, Timeout.InfiniteTimeSpan);
                }

                // this instance may already be completed (success or cancelled) at this point, but a race condition
                // may have happened where we allocated for the CT or for the timer, make sure we dispose them.
                if (Task.IsCompleted)
                    Dispose();
            }

            private static void RawOnCancelRequested(object s)
            {
                ((EventWaiter)s).OnCancelRequested();
            }

            private void OnCancelRequested()
            {
                if (TrySetCanceled())
                    m_owner.Cancel(this);
            }

            public void Dispose()
            {
                _ = m_ctRegistration.DisposeAsync();
                _ = m_timer?.DisposeAsync();
                TrySetCanceled();
            }
        }

        protected override void OnDispose()
        {
            lock (m_lock)
            {
                while (m_firstWaiter != null)
                {
                    m_firstWaiter.TrySetCanceled();
                    m_firstWaiter = m_firstWaiter.Previous;
                }
            }

            base.OnDispose();
        }

        private void Cancel(EventWaiter eventWaiter)
        {
            lock (m_lock)
            {
                if (eventWaiter.Next != null)
                    eventWaiter.Next.Previous = eventWaiter.Previous;
                if (eventWaiter.Previous != null)
                    eventWaiter.Previous.Next = eventWaiter.Next;

                if (ReferenceEquals(m_firstWaiter, eventWaiter))
                    m_firstWaiter = eventWaiter.Previous;
            }
        }
    }
}
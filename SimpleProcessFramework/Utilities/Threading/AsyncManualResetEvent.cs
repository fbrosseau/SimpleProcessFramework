using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
    internal class AsyncManualResetEvent : Disposable
    {
        private readonly object m_lock = new object();

        private EventWaiter m_firstWaiter;
        private EventWaiter m_noCancellationWaiters;

        private Exception m_disposeException;

        public bool IsSet { get; private set; }

        private class EventWaiter : TaskCompletionSource<bool>, IDisposable
        {
            public EventWaiter Next;
            public EventWaiter Previous;
            private IDisposable m_ctRegistration; // boxing to avoid tearing
            private Timer m_timer;
            private readonly AsyncManualResetEvent m_owner;

            private static readonly TimerCallback s_rawTimeoutRequest = RawOnCancelRequested;
            private static readonly Action<object> s_rawCancelRequest = RawOnCancelRequested;

            public EventWaiter(AsyncManualResetEvent e)
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
                m_owner = e;
            }

            internal void Activate(CancellationToken ct, TimeSpan timeout)
            {
                if (ct.CanBeCanceled)
                {
                    m_ctRegistration = ct.Register(s_rawCancelRequest, this, false);
                }

                if (timeout != Timeout.InfiniteTimeSpan)
                {
                    m_timer = new Timer(s_rawTimeoutRequest, this, timeout, Timeout.InfiniteTimeSpan);
                }

                if (Task.IsCompleted)
                    Dispose();
            }

            private static void RawOnCancelRequested(object s)
            {
                ((EventWaiter)s).OnCancelRequested();
            }

            private void OnCancelRequested()
            {
                TrySetCanceled();
                m_owner.Cancel(this);
            }

            public void Dispose()
            {
                TrySetCanceled();
                Thread.MemoryBarrier();
                m_ctRegistration?.Dispose();
                m_timer?.Dispose();
            }
        }

        public AsyncManualResetEvent(bool initialState = false)
        {
            IsSet = initialState;
        }

        public void Set()
        {
            lock (m_lock)
            {
                if (IsSet)
                    return;

                IsSet = true;
                UnblockAllWaiters(true);
            }
        }

        public void Reset()
        {
            lock (m_lock)
            {
                IsSet = false;
            }
        }

        public async ValueTask WaitAsync()
        {
            await WaitAsync(CancellationToken.None);
        }

        public ValueTask<bool> WaitAsync(CancellationToken ct)
        {
            return WaitAsync(ct, Timeout.InfiniteTimeSpan);
        }

        public ValueTask<bool> WaitAsync(TimeSpan timeout)
        {
            return WaitAsync(CancellationToken.None, timeout);
        }

        protected override void OnDispose()
        {
            lock (m_lock)
            {
                UnblockAllWaiters(false);
            }

            base.OnDispose();
        }

        public void Dispose(Exception ex)
        {
            m_disposeException = ex;
            Dispose();
        }

        private ValueTask<bool> WaitAsync(CancellationToken ct, TimeSpan timeout, EventWaiter waiter = null)
        {
            bool cancellable = timeout != Timeout.InfiniteTimeSpan || ct.CanBeCanceled;
            bool cancelled = ct.IsCancellationRequested || timeout == TimeSpan.Zero;

            lock (m_lock)
            {
                ThrowIfDisposed();

                if (IsSet)
                    return new ValueTask<bool>(true);

                if (cancelled)
                    return new ValueTask<bool>(false);

                if (!cancellable)
                {
                    if (m_noCancellationWaiters != null)
                        return new ValueTask<bool>(m_noCancellationWaiters.Task);

                    if (waiter != null)
                        m_noCancellationWaiters = waiter;
                }

                if (waiter != null)
                {
                    if (m_firstWaiter != null)
                        m_firstWaiter.Next = waiter;

                    waiter.Previous = m_firstWaiter;
                    m_firstWaiter = waiter;
                }
            }

            if (waiter != null)
            {
                waiter.Activate(ct, timeout);
                return new ValueTask<bool>(waiter.Task);
            }

            return WaitAsync(ct, timeout, new EventWaiter(this));
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

        private void UnblockAllWaiters(bool success)
        {
            var ex = m_disposeException;

            Debug.Assert(Monitor.IsEntered(m_lock));
            while (m_firstWaiter != null)
            {
                if (success)
                {
                    m_firstWaiter.TrySetResult(true);
                }
                else
                {
                    if (ex is null)
                        m_firstWaiter.TrySetCanceled();
                    else
                        m_firstWaiter.TrySetException(new ObjectDisposedException("This object is disposed", ex));
                }

                m_firstWaiter = m_firstWaiter.Previous;
            }

            m_noCancellationWaiters = null;
        }
    }
}
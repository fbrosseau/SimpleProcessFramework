using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
    internal class AsyncManualResetEvent : BaseAsyncEvent
    {
        private EventWaiter m_noCancellationWaiters;

        public bool IsSet { get; private set; }

        public AsyncManualResetEvent(bool initialState = false)
        {
            IsSet = initialState;
        }

        protected override void OnDispose()
        {
            lock (m_lock)
            {
                UnblockAllWaiters(false);
            }

            base.OnDispose();
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

        public ValueTask WaitAsync()
        {
            return WaitInternal(default, Timeout.InfiniteTimeSpan).AsVoidValueTask();
        }
        public ValueTask<bool> WaitAsync(CancellationToken ct)
        {
            return WaitInternal(ct, Timeout.InfiniteTimeSpan).AsValueTask();
        }
        public ValueTask<bool> WaitAsync(TimeSpan timeout)
        {
            return WaitInternal(CancellationToken.None, timeout).AsValueTask();
        }

        private WaitSyncOrAsyncResult WaitInternal(CancellationToken ct, TimeSpan timeout, EventWaiter waiter = null)
        {
            bool cancellable = timeout != Timeout.InfiniteTimeSpan || ct.CanBeCanceled;
            bool cancelled = ct.IsCancellationRequested || timeout == TimeSpan.Zero;

            lock (m_lock)
            {
                ThrowIfDisposed();

                if (IsSet)
                    return new WaitSyncOrAsyncResult(true);

                if (cancelled)
                    return new WaitSyncOrAsyncResult(false);

                if (!cancellable)
                {
                    if (m_noCancellationWaiters != null)
                        return new WaitSyncOrAsyncResult(m_noCancellationWaiters.Task);

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
                return new WaitSyncOrAsyncResult(waiter.Task);
            }

            return WaitInternal(ct, timeout, new EventWaiter(this));
        }

        private void UnblockAllWaiters(bool success)
        {
            Debug.Assert(Monitor.IsEntered(m_lock));

            while (m_firstWaiter != null)
            {
                if (success)
                    m_firstWaiter.TrySetResult(true);
                else
                    m_firstWaiter.TrySetCanceled();

                m_firstWaiter = m_firstWaiter.Previous;
            }

            m_noCancellationWaiters = null;
        }
    }
}
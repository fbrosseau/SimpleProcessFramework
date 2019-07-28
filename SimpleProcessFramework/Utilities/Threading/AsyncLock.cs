using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
    internal sealed class AsyncLock : Disposable
    {
        private class LockSession : ValueTaskCompletionSource<IDisposable>, IDisposable, IThreadPoolWorkItem
        {
            private AsyncLock m_asyncLock;
            private volatile bool m_markAsCancelled;

            public LockSession(AsyncLock asyncLock)
            {
                m_asyncLock = asyncLock;
            }

            void IThreadPoolWorkItem.Execute()
            {
                if (m_markAsCancelled)
                    TrySetCanceled();
                else
                    Unblock(true);
            }

            public void Unblock(bool canCompleteSynchronously = false)
            {
                if (canCompleteSynchronously)
                    TrySetResult(this);
                else
                    ThreadPoolHelper.QueueItem(this);
            }

            public void Cancel(bool canCompleteSynchronously)
            {
                if (canCompleteSynchronously)
                {
                    TrySetCanceled();
                }
                else
                {
                    m_markAsCancelled = true;
                    ThreadPoolHelper.QueueItem(this);
                }
            }

            void IDisposable.Dispose()
            {
                // it's very important that we don't ExitLock more than once
                // from this instance! 
                var lockInstance = Interlocked.Exchange(ref m_asyncLock, null);
                lockInstance?.ExitLock();
            }
        }

        private readonly Queue<LockSession> m_waiters;

        public bool IsLockTaken { get; private set; }

        public AsyncLock()
        {
            m_waiters = new Queue<LockSession>();
        }

        public ValueTask<IDisposable> LockAsync()
        {
            var session = new LockSession(this);

            lock (m_waiters)
            {
                if (IsDisposed)
                {
                    session.Cancel(canCompleteSynchronously: true);
                }
                else if (IsLockTaken)
                {
                    m_waiters.Enqueue(session);
                }
                else
                {
                    IsLockTaken = true;
                    session.Unblock(canCompleteSynchronously: true);
                }
            }

            return session.ValueTask;
        }

        private void ExitLock()
        {
            LockSession completedWaiter = null;

            lock (m_waiters)
            {
                if (m_waiters.Count > 0)
                {
                    completedWaiter = m_waiters.Dequeue();
                }
                else
                {
                    IsLockTaken = false;
                }
            }

            completedWaiter?.Unblock();
        }

        protected override void OnDispose()
        {
            List<LockSession> waiters = null;
            lock (m_waiters)
            {
                if (m_waiters.Count > 0)
                    waiters = m_waiters.ToList();
            }

            if (waiters != null)
            {
                ThreadPool.QueueUserWorkItem(s =>
                {
                    foreach (var waiter in (List<LockSession>)s)
                    {
                        waiter.Cancel(canCompleteSynchronously: true);
                    }
                }, waiters);
            }

            base.OnDispose();
        }
    }
}
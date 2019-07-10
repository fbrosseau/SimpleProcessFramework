﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
    internal sealed class AsyncLock : Disposable
    {
        private class LockSession : TaskCompletionSource<IDisposable>, IDisposable
        {
            private AsyncLock m_asyncLock;

            public LockSession(AsyncLock asyncLock)
            {
                m_asyncLock = asyncLock;
            }

            public void Unblock(bool canCompleteSynchronously = false)
            {
                if (canCompleteSynchronously)
                    TrySetResult(this);
                else
                    ThreadPool.QueueUserWorkItem(s => ((LockSession)s).Unblock(true), this);
            }

            public void Cancel(bool canCompleteSynchronously, Func<Exception> exceptionFactory = null)
            {
                if (canCompleteSynchronously)
                {
                    if (exceptionFactory == null)
                        TrySetCanceled();
                    else
                        TrySetException(exceptionFactory());
                }
                else
                {
                    ThreadPool.QueueUserWorkItem(s => ((LockSession)s).Cancel(true, exceptionFactory), this);
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

        public Task<IDisposable> LockAsync()
        {
            LockSession session = new LockSession(this);

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

            return session.Task;
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
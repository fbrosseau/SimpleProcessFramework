using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Spfx.Utilities.Threading
{
    internal sealed class AsyncLock : Disposable
    {
        private readonly FreeLockSession m_freeLockSession;
        private readonly Queue<LockSession> m_waiters;
        private object m_activeLockSession;

        public bool IsLockTaken { get; private set; }

        public AsyncLock()
        {
            m_waiters = new Queue<LockSession>();
            m_freeLockSession = new FreeLockSession(this);
        }

        public ValueTask<IDisposable> LockAsync()
        {
            return LockInternal(null);
        }

        private ValueTask<IDisposable> LockInternal(LockSession lockSession)
        {
            if (IsLockTaken && lockSession is null)
                lockSession = new LockSession(this);

            object rawLockSession = lockSession;

            lock (m_waiters)
            {
                if (IsDisposed)
                {
                    lockSession.Cancel(canCompleteSynchronously: true);
                }
                else if (IsLockTaken)
                {
                    m_waiters.Enqueue(lockSession);
                }
                else
                {
                    IsLockTaken = true;

                    if (lockSession is null)
                        rawLockSession = m_freeLockSession;
                    else
                        lockSession.Unblock(canCompleteSynchronously: true);

                    m_activeLockSession = rawLockSession;
                }
            }

            if (rawLockSession is null)
                return LockInternal(new LockSession(this));

            if (rawLockSession is FreeLockSession freeLock)
                return freeLock.CreateNextValueTask(this);

            return new ValueTask<IDisposable>(lockSession.Task);
        }

        private void ExitLock(object completedSession)
        {
            LockSession completedWaiter = null;

            lock (m_waiters)
            {
                if (m_waiters.Count > 0)
                    completedWaiter = m_waiters.Dequeue();
                else
                    IsLockTaken = false;

                m_activeLockSession = completedSession;
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

        private class LockSession : TaskCompletionSource<IDisposable>, IDisposable, IThreadPoolWorkItem
        {
            protected AsyncLock m_asyncLock;
            private volatile bool m_completeSynchronously;

            public LockSession(AsyncLock asyncLock)
            {
                m_asyncLock = asyncLock;
            }

            void IThreadPoolWorkItem.Execute()
            {
                if (!m_completeSynchronously)
                    TrySetCanceled();
                else
                    Unblock(true);
            }

            public void Unblock(bool canCompleteSynchronously = false)
            {
                if (!canCompleteSynchronously)
                    RequeueThis(true);
                else
                    TrySetResult(this);
            }

            public void Cancel(bool canCompleteSynchronously)
            {
                if (!canCompleteSynchronously)
                    RequeueThis(false);
                else
                    TrySetCanceled();
            }

            private void RequeueThis(bool completedSuccessfully)
            {
                m_completeSynchronously = completedSuccessfully;
                ThreadPoolHelper.QueueItem(this);
            }

            void IDisposable.Dispose()
            {
                // it's very important that we don't ExitLock more than once
                // from this instance! 
                var lockInstance = m_asyncLock != null ? Interlocked.Exchange(ref m_asyncLock, null) : null;
                lockInstance?.ExitLock(this);
            }
        }

        private class FreeLockSession : IValueTaskSource<IDisposable>, IDisposable, IThreadPoolWorkItem
        {
            private AsyncLock m_asyncLock;
            private short m_token;

            private Action<object> m_continuationToInvoke;
            private object m_continuationToInvokeState;

            public FreeLockSession(AsyncLock asyncLock)
            {
                m_asyncLock = asyncLock;
            }

            internal ValueTask<IDisposable> CreateNextValueTask(AsyncLock asyncLock)
            {
                m_asyncLock = asyncLock;
                m_continuationToInvoke = null;
                return new ValueTask<IDisposable>(this, ++m_token);
            }

            void IDisposable.Dispose()
            {
                // it's very important that we don't ExitLock more than once
                // from this instance! 
                var lockInstance = m_asyncLock != null ? Interlocked.Exchange(ref m_asyncLock, null) : null;
                lockInstance?.ExitLock(this);
            }

            IDisposable IValueTaskSource<IDisposable>.GetResult(short token)
            {
                CheckToken(token);

                m_continuationToInvoke = null;
                m_continuationToInvokeState = null;

                return this;
            }

            ValueTaskSourceStatus IValueTaskSource<IDisposable>.GetStatus(short token)
            {
                CheckToken(token);
                return ValueTaskSourceStatus.Succeeded;
            }

            void IValueTaskSource<IDisposable>.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
            {
                CheckToken(token);

                if (Interlocked.Exchange(ref m_continuationToInvoke, continuation) != null)
                    ThrowInvalidOperationException();

                m_continuationToInvoke = continuation;
                m_continuationToInvokeState = state;

                if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
                {
                    var sc = SynchronizationContext.Current;
                    if (sc != null && sc.GetType() != typeof(SynchronizationContext))
                    {
                        sc.Post(s =>
                        {
                            ((FreeLockSession)s).InvokeCallback();
                        }, this);
                        return;
                    }

                    var ts = TaskScheduler.Current;
                    if (ts != TaskScheduler.Default)
                    {
                        Task.Factory.StartNew(continuation, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, ts);
                        return;
                    }
                }

                if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
                {
#if NETCOREAPP2_1_PLUS || NETSTANDANDARD2_1_PLUS
                    ThreadPool.QueueUserWorkItem(continuation, state, preferLocal: true);
#else
                    ThreadPool.QueueUserWorkItem(s => ((FreeLockSession)s).InvokeCallback(), this);;
#endif
                }
                else
                {
#if NETCOREAPP3_0_PLUS || NETSTANDARD2_1_PLUS
                    ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: true);
#else
                    ThreadPool.UnsafeQueueUserWorkItem(s => ((FreeLockSession)s).InvokeCallback(), this);
#endif
                }
            }

            private void CheckToken(short token)
            {
                if (token != m_token)
                    ThrowInvalidOperationException();
            }

            private void ThrowInvalidOperationException()
            {
                throw new InvalidOperationException();
            }

            private void InvokeCallback()
            {
                m_continuationToInvoke(m_continuationToInvokeState);
            }

            void IThreadPoolWorkItem.Execute()
            {
                InvokeCallback();
            }
        }
    }
}
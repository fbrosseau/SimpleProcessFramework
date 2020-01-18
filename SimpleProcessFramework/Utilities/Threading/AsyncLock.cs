using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace Spfx.Utilities.Threading
{
    internal sealed class AsyncLock : Disposable
    {
        private readonly FreeLockSession m_freeLockSession;
        private readonly Queue<LockSession> m_waiters;

        [SuppressMessage("Code Quality", "IDE0052:Remove unread private members", Justification = "for debugging")]
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
                    lockSession?.CancelDueToDispose();
                }
                else if (IsLockTaken)
                {
                    if (lockSession != null)
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
            LockSession[] waiters = null;
            lock (m_waiters)
            {
                if (m_waiters.Count > 0)
                {
                    waiters = m_waiters.ToArray();
                    m_waiters.Clear();
                }
            }

            if (waiters != null)
            {
                ThreadPool.QueueUserWorkItem(s =>
                {
                    foreach (var waiter in (LockSession[])s)
                    {
                        waiter.CancelDueToDispose();
                    }
                }, waiters);
            }

            base.OnDispose();
        }

        private class LockSession : TaskCompletionSource<IDisposable>, IDisposable
        {
            private AsyncLock m_asyncLock;
            private volatile bool m_completeSynchronously;
            private readonly ThreadPoolInvoker<LockSessionInvoker> m_threadpoolInvoker;

            public LockSession(AsyncLock asyncLock)
            {
                m_asyncLock = asyncLock;
                m_threadpoolInvoker = ThreadPoolInvoker.Create(new LockSessionInvoker { Parent = this });
            }

            private void FinalCallback()
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

            public void CancelDueToDispose()
            {
                TrySetException(new ObjectDisposedException(nameof(AsyncLock)));
            }

            private void RequeueThis(bool completedSuccessfully)
            {
                m_completeSynchronously = completedSuccessfully;
                m_threadpoolInvoker.UnsafeInvoke();
            }

            void IDisposable.Dispose()
            {
                // it's very important that we don't ExitLock more than once
                // from this instance! 
                var lockInstance = m_asyncLock != null ? Interlocked.Exchange(ref m_asyncLock, null) : null;
                lockInstance?.ExitLock(this);
            }

            private struct LockSessionInvoker : IThreadPoolWorkItem
            {
                public LockSession Parent;
                public void Execute() => Parent.FinalCallback();
            }
        }

        private sealed class FreeLockSession : IValueTaskSource<IDisposable>, IDisposable, IThreadPoolWorkItem
        {
            private AsyncLock m_asyncLock;
            private short m_token;

            private Action<object> m_continuationToInvoke;
            private object m_continuationToInvokeState;
            private readonly ThreadPoolInvoker<LockSessionInvoker> m_threadpoolInvoker;

            private struct LockSessionInvoker : IThreadPoolWorkItem
            {
                public FreeLockSession Parent;
                public void Execute() => Parent.InvokeCallback();
            }

            public FreeLockSession(AsyncLock asyncLock)
            {
                m_asyncLock = asyncLock;
                m_threadpoolInvoker = ThreadPoolInvoker.Create(new LockSessionInvoker { Parent = this });
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

                m_threadpoolInvoker.InvokeValueTaskCompletion(flags);
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
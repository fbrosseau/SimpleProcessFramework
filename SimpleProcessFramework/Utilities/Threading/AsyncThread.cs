using Spfx.Diagnostics;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
    internal class AsyncThread : AsyncDestroyable
    {
        private readonly AsyncLock m_lock;
        private readonly bool m_allowSyncCompletion;

        public AsyncThread(bool allowSyncCompletion = false)
        {
            m_lock = new AsyncLock();
            m_allowSyncCompletion = allowSyncCompletion;
        }

        protected override void OnDispose()
        {
            m_lock.Dispose();
            base.OnDispose();
        }

        public ValueTask ExecuteActionAsync(Action action) => ExecuteActionAsync(ActionDelegateInvoker.Create(action));
        public ValueTask ExecuteActionAsync<T1>(Action<T1> action, T1 a1) => ExecuteActionAsync(ActionDelegateInvoker.Create(action, a1));
        public ValueTask<TReturn> ExecuteAsync<TReturn>(Func<TReturn> action) => ExecuteFuncAsync<TReturn, FuncDelegateInvoker<TReturn>>(ActionDelegateInvoker.Create(action));
        public ValueTask<TReturn> ExecuteAsync<TReturn, T1>(Func<T1, TReturn> action, T1 a1) => ExecuteFuncAsync<TReturn, FuncDelegateInvoker<T1, TReturn>>(ActionDelegateInvoker.Create(action, a1));

        public void QueueAction(Action action) => QueueAction(ActionDelegateInvoker.Create(action));
        public void QueueAction<T1>(Action<T1> action, T1 a1) => QueueAction(ActionDelegateInvoker.Create(action, a1));

        public void QueueTask(Func<Task> func) => QueueTask(ActionDelegateInvoker.Create(func));
        public void QueueTask<T1>(Func<T1, Task> func, T1 a1) => QueueTask(ActionDelegateInvoker.Create(func, a1));
        public void QueueValueTask(Func<ValueTask> func) => QueueValueTask(ActionDelegateInvoker.Create(func));
        public void QueueValueTask<T1>(Func<T1, ValueTask> func, T1 a1) => QueueValueTask(ActionDelegateInvoker.Create(func, a1));

        public ValueTask ExecuteTaskAsync(Func<Task> action) => ExecuteTaskAsync(ActionDelegateInvoker.Create(action));
        public ValueTask ExecuteValueTaskAsync(Func<ValueTask> action) => ExecuteValueTaskAsync(ActionDelegateInvoker.Create(action));
        public ValueTask<TReturn> ExecuteTaskAsync<TReturn>(Func<Task<TReturn>> action) => ExecuteTaskAsync<TReturn, FuncDelegateInvoker<Task<TReturn>>>(ActionDelegateInvoker.Create(action));
        public ValueTask<TReturn> ExecuteValueAsync<TReturn>(Func<ValueTask<TReturn>> action) => ExecuteValueTaskAsync<TReturn, FuncDelegateInvoker<ValueTask<TReturn>>>(ActionDelegateInvoker.Create(action));

        protected struct ExecutionReadyAwaitable
        {
            private readonly ValueTask<IDisposable> m_acquireLockTask;
            private readonly bool m_allowSyncCompletion;

            public ExecutionReadyAwaitable(ValueTask<IDisposable> valueTask, bool allowSyncCompletion)
            {
                m_acquireLockTask = valueTask;
                m_allowSyncCompletion = allowSyncCompletion;
            }

            public ExecutionReadyAwaiter GetAwaiter()
            {
                return new ExecutionReadyAwaiter(m_acquireLockTask, m_allowSyncCompletion);
            }
        }

        protected struct ExecutionReadyAwaiter : ICriticalNotifyCompletion
        {
            private readonly ConfiguredValueTaskAwaitable<IDisposable>.ConfiguredValueTaskAwaiter m_acquireLockTask;
            private readonly bool m_allowSyncCompletion;

            public ExecutionReadyAwaiter(ValueTask<IDisposable> acquireLockTask, bool allowSyncCompletion)
            {
                m_acquireLockTask = acquireLockTask.ConfigureAwait(false).GetAwaiter();
                m_allowSyncCompletion = allowSyncCompletion;

                if (!allowSyncCompletion)
                    IsCompleted = false;
                else
                    IsCompleted = acquireLockTask.IsCompleted;
            }

            public bool IsCompleted { get; }

            public IDisposable GetResult()
            {
                return m_acquireLockTask.GetResult();
            }

            public void OnCompleted(Action continuation)
            {
                if (m_allowSyncCompletion)
                {
                    m_acquireLockTask.OnCompleted(continuation);
                }
                else 
                {
                    var copy = m_acquireLockTask;
                    ThreadPool.QueueUserWorkItem(s =>
                    {
                        copy.OnCompleted(continuation);
                    });
                }
            }

            public void UnsafeOnCompleted(Action continuation)
            {
                if (m_allowSyncCompletion)
                {
                    m_acquireLockTask.UnsafeOnCompleted(continuation);
                }
                else
                {
                    var copy = m_acquireLockTask;
                    ThreadPool.QueueUserWorkItem(s =>
                    {
                        copy.UnsafeOnCompleted(continuation);
                    });
                }
            }
        }

        protected virtual ExecutionReadyAwaitable WaitForTurn()
        {
            return new ExecutionReadyAwaitable(m_lock.LockAsync(), m_allowSyncCompletion);
        }

        public async ValueTask ExecuteActionAsync<TDelegateInvoker>(TDelegateInvoker delegateInvoker)
           where TDelegateInvoker : IDelegateInvoker
        {
            using (await WaitForTurn())
            {
                delegateInvoker.Invoke();
            }
        }

        public async ValueTask<TReturn> ExecuteFuncAsync<TReturn, TDelegateInvoker>(TDelegateInvoker delegateInvoker)
           where TDelegateInvoker : IFuncInvoker<TReturn>
        {
            using (await WaitForTurn())
            {
                return delegateInvoker.Invoke();
            }
        }

        public async ValueTask<TReturn> ExecuteTaskAsync<TReturn, TDelegateInvoker>(TDelegateInvoker delegateInvoker)
           where TDelegateInvoker : IFuncInvoker<Task<TReturn>>
        {
            using (await WaitForTurn())
            {
                return await delegateInvoker.Invoke().ConfigureAwait(false);
            }
        }

        public async ValueTask<TReturn> ExecuteValueTaskAsync<TReturn, TDelegateInvoker>(TDelegateInvoker delegateInvoker)
           where TDelegateInvoker : IFuncInvoker<ValueTask<TReturn>>
        {
            using (await WaitForTurn())
            {
                return await delegateInvoker.Invoke().ConfigureAwait(false);
            }
        }

        public async ValueTask ExecuteTaskAsync<TDelegateInvoker>(TDelegateInvoker delegateInvoker)
           where TDelegateInvoker : IFuncInvoker<Task>
        {
            using (await WaitForTurn())
            {
                await delegateInvoker.Invoke().ConfigureAwait(false);
            }
        }

        public async ValueTask ExecuteValueTaskAsync<TDelegateInvoker>(TDelegateInvoker delegateInvoker)
           where TDelegateInvoker : IFuncInvoker<ValueTask>
        {
            using (await WaitForTurn())
            {
                await delegateInvoker.Invoke().ConfigureAwait(false);
            }
        }

        public async void QueueAction<TDelegateInvoker>(TDelegateInvoker delegateInvoker)
            where TDelegateInvoker : IDelegateInvoker
        {
            try
            {
                await ExecuteActionAsync(delegateInvoker).ConfigureAwait(false);
            }
            catch (Exception ex) when (FilterUnhandledException(ex))
            {
                OnUnhandledException(ex);
            }
        }

        public async void QueueValueTask<TDelegateInvoker>(TDelegateInvoker delegateInvoker)
               where TDelegateInvoker : IFuncInvoker<ValueTask>
        {
            try
            {
                await ExecuteValueTaskAsync(delegateInvoker).ConfigureAwait(false);
            }
            catch (Exception ex) when (FilterUnhandledException(ex))
            {
                OnUnhandledException(ex);
            }
        }

        public async void QueueTask<TDelegateInvoker>(TDelegateInvoker delegateInvoker)
               where TDelegateInvoker : IFuncInvoker<Task>
        {
            try
            {
                await ExecuteTaskAsync(delegateInvoker).ConfigureAwait(false);
            }
            catch (Exception ex) when (FilterUnhandledException(ex))
            {
                OnUnhandledException(ex);
            }
        }

        private void OnUnhandledException(Exception ex)
        {
            // TODO
        }

        private bool FilterUnhandledException(Exception ex)
        {
            Debug.Fail("Unhandled ex: " + ex);
            return true;
        }
    }
}

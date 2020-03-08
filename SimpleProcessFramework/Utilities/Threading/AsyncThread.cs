using System;
using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
    internal class AsyncThread
    {
        private readonly AsyncLock m_lock;

        internal AsyncThread()
        {
            m_lock = new AsyncLock();
        }

        public ValueTask ExecuteAsync(Action action) => ExecuteActionAsync(ActionDelegateInvoker.Create(action));
        public ValueTask ExecuteAsync<T1>(Action<T1> action, T1 state) => ExecuteActionAsync(ActionDelegateInvoker.Create(action, state));
        public ValueTask<TReturn> ExecuteAsync<TReturn>(Func<TReturn> action) => ExecuteFuncAsync<TReturn, FuncDelegateInvoker<TReturn>>(ActionDelegateInvoker.Create(action));
        public ValueTask<TReturn> ExecuteAsync<TReturn, T1>(Func<T1, TReturn> action, T1 state) => ExecuteFuncAsync<TReturn, FuncDelegateInvoker<T1, TReturn>>(ActionDelegateInvoker.Create(action, state));

        public ValueTask ExecuteAsync(Func<Task> action) => ExecuteTaskAsync(ActionDelegateInvoker.Create(action));
        public ValueTask ExecuteAsync(Func<ValueTask> action) => ExecuteValueTaskAsync(ActionDelegateInvoker.Create(action));
        public ValueTask<TReturn> ExecuteAsync<TReturn>(Func<Task<TReturn>> action) => ExecuteTaskAsync<TReturn, FuncDelegateInvoker<Task<TReturn>>>(ActionDelegateInvoker.Create(action));
        public ValueTask<TReturn> ExecuteAsync<TReturn>(Func<ValueTask<TReturn>> action) => ExecuteValueTaskAsync<TReturn, FuncDelegateInvoker<ValueTask<TReturn>>>(ActionDelegateInvoker.Create(action));

        public async ValueTask ExecuteActionAsync<TDelegateInvoker>(TDelegateInvoker delegateInvoker)
           where TDelegateInvoker : IDelegateInvoker
        {
            using (await m_lock.LockAsync().ConfigureAwait(false))
            {
                delegateInvoker.Invoke();
            }
        }

        public async ValueTask<TReturn> ExecuteFuncAsync<TReturn, TDelegateInvoker>(TDelegateInvoker delegateInvoker)
           where TDelegateInvoker : IFuncInvoker<TReturn>
        {
            using (await m_lock.LockAsync().ConfigureAwait(false))
            {
                return delegateInvoker.Invoke();
            }
        }

        public async ValueTask<TReturn> ExecuteTaskAsync<TReturn, TDelegateInvoker>(TDelegateInvoker delegateInvoker)
           where TDelegateInvoker : IFuncInvoker<Task<TReturn>>
        {
            using (await m_lock.LockAsync().ConfigureAwait(false))
            {
                return await delegateInvoker.Invoke().ConfigureAwait(false);
            }
        }

        public async ValueTask<TReturn> ExecuteValueTaskAsync<TReturn, TDelegateInvoker>(TDelegateInvoker delegateInvoker)
           where TDelegateInvoker : IFuncInvoker<ValueTask<TReturn>>
        {
            using (await m_lock.LockAsync().ConfigureAwait(false))
            {
                return await delegateInvoker.Invoke().ConfigureAwait(false);
            }
        }

        public async ValueTask ExecuteTaskAsync<TDelegateInvoker>(TDelegateInvoker delegateInvoker)
           where TDelegateInvoker : IFuncInvoker<Task>
        {
            using (await m_lock.LockAsync().ConfigureAwait(false))
            {
                await delegateInvoker.Invoke().ConfigureAwait(false);
            }
        }

        public async ValueTask ExecuteValueTaskAsync<TDelegateInvoker>(TDelegateInvoker delegateInvoker)
           where TDelegateInvoker : IFuncInvoker<ValueTask>
        {
            using (await m_lock.LockAsync().ConfigureAwait(false))
            {
                await delegateInvoker.Invoke().ConfigureAwait(false);
            }
        }
    }
}

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
    internal static class AsyncLazy
    {
        public static AsyncLazy<T> Create<T>(Func<Task<T>> factory)
        {
            return new AsyncLazy<T>(factory);
        }
    }

    internal class AsyncLazy<T>
    {
        public Task<T> ResultTask => GetTask();
        private Task<Task<T>> m_originalTask;
        private Task<T> m_task;

        public T SynchronousResult => ResultTask.GetResultOrRethrow();

        public AsyncLazy(Func<T> factory)
            : this(() => Task.FromResult(factory()))
        {
        }
        
        public AsyncLazy(Func<Task<T>> factory)
        {
            m_originalTask = new Task<Task<T>>(() =>
            {
                try
                {
                    var res = factory();
                    res.ContinueWith((t, s) =>
                    {
                        ((AsyncLazy<T>)s).OnInnerTaskCompleted(t);
                    }, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

                    return res;
                }
                catch (Exception ex)
                {
                    OnFailure(ex);
                    throw;
                }
            });
        }

        private void OnInnerTaskCompleted(Task<T> t)
        {
            if (t.IsCompletedSuccessfully())
            {
                m_task = Task.FromResult(t.Result);
            }
            else
            {
                OnFailure(t.GetExceptionOrCancel());
            }
        }

        private void OnFailure(Exception ex)
        {
            m_task = Task.FromException<T>(ex);
        }

        private Task<T> GetTask()
        {
            var task = m_task;
            if (task != null)
                return task;

            var unwrap = m_originalTask?.Unwrap();
            if (unwrap is null)
            {
                while (m_task is null)
                    Thread.Yield();
                return m_task;
            }

            task = Interlocked.CompareExchange(ref m_task, unwrap, null);
            if (task != null)
                return task;

            m_originalTask.Start(TaskScheduler.Default);
            m_originalTask = null;

            return unwrap;
        }

        public TaskAwaiter<T> GetAwaiter() => ResultTask.GetAwaiter();
    }
}
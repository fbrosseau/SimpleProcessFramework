using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable;

namespace Spfx.Utilities.Threading
{
    internal sealed class AsyncQueue<T> : IDisposable
    {
        private class Waiter : TaskCompletionSource<T>
        {
            public Waiter()
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
            }

            public void SetDequeueResult(T item)
            {
                TrySetResult(item);
            }
        }

        private readonly Queue<T> m_queue;
        private readonly Queue<Waiter> m_waiters;
        private Exception m_disposeException;

        private bool m_isIteratorMode;
        private bool m_isInIteratorCallback;

        private TaskCompletionSource<VoidType> m_iteratorCompletionTask;
        private Action m_iteratorExecutionCompletedHandler;
        private FastThreadpoolInvoker<AsyncQueueRescheduleItem> m_iteratorThreadpoolInvoker;
        private Action<T> m_iteratorActionCallback;
        private Func<T, Task> m_iteratorTaskCallback;
        private Func<T, ValueTask> m_iteratorValueTaskCallback;
        private ConfiguredValueTaskAwaiter m_currentIteratorCallbackCompletion;

        public bool IsAddingCompleted { get; private set; }
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// If set to 'True', items that are not raised after disposing the queue
        /// will be disposed if they implement IDisposable.
        /// </summary>
        public bool DisposeIgnoredItems { get; set; }

        public AsyncQueue()
        {
            m_queue = new Queue<T>();
            m_waiters = new Queue<Waiter>();
        }

        public void Enqueue(T item)
        {
            Waiter completedWaiter = null;
            bool isAddingCompleted = false;
            bool scheduleIterator = false;

            lock (m_queue)
            {
                if (IsAddingCompleted)
                {
                    isAddingCompleted = true;
                }
                else
                {
                    if (m_isIteratorMode)
                    {
                        m_queue.Enqueue(item);
                        if (!m_isInIteratorCallback)
                        {
                            m_isInIteratorCallback = true;
                            scheduleIterator = true;
                        }
                    }
                    else if (m_waiters.Count > 0)
                    {
                        completedWaiter = m_waiters.Dequeue();
                    }
                    else
                    {
                        m_queue.Enqueue(item);
                    }
                }
            }

            if (scheduleIterator)
            {
                RescheduleIterator();
                return;
            }

            if (isAddingCompleted && DisposeIgnoredItems)
                TryDisposeItem(item);

            completedWaiter?.SetDequeueResult(item);
        }

        private void RescheduleIterator()
        {
            m_iteratorThreadpoolInvoker.Invoke();
        }

        private void RunIterator()
        {
            try
            {
                int remainingIterations = 10;
                while (--remainingIterations > 0)
                {
                    T item;
                    lock (m_queue)
                    {
                        item = m_queue.Dequeue();
                    }

                    var completion = new ValueTask();
                    if (m_iteratorActionCallback != null)
                    {
                        m_iteratorActionCallback(item);
                    }
                    else if (m_iteratorTaskCallback != null)
                    {
                        completion = new ValueTask(m_iteratorTaskCallback(item));
                    }
                    else
                    {
                        completion = m_iteratorValueTaskCallback(item);
                    }

                    if (!completion.IsCompleted)
                    {
                        var awaiter = completion.ConfigureAwait(false).GetAwaiter();
                        m_currentIteratorCallbackCompletion = awaiter;
                        awaiter.UnsafeOnCompleted(m_iteratorExecutionCompletedHandler);
                        return;
                    }

                    if (!ContinueDequeue())
                        return;
                }

                RescheduleIterator();
            }
            catch (Exception ex)
            {
                OnIteratorFaulted(ex);
            }
        }

        private void OnIteratorFaulted(Exception ex)
        {
            lock(m_queue)
            {
                m_isInIteratorCallback = false;
            }

            m_iteratorCompletionTask.TrySetException(ex);
        }

        private void OnIteratorCallbackCompleted()
        {
            var compl = m_currentIteratorCallbackCompletion;
            m_currentIteratorCallbackCompletion = default;

            try
            {
                compl.GetResult();
            }
            catch (Exception ex)
            {
                OnIteratorFaulted(ex);
                return;
            }

            if (ContinueDequeue())
                RescheduleIterator();
        }

        private bool ContinueDequeue()
        {
            bool rescheduleIterator = false;
            lock (m_queue)
            {
                if (IsAddingCompleted)
                {
                    m_isInIteratorCallback = false;
                    m_iteratorCompletionTask.TrySetResult(VoidType.Value);
                }
                else if (m_queue.Count > 0)
                {
                    rescheduleIterator = true;
                }
                else
                {
                    m_isInIteratorCallback = false;
                }
            }

            return rescheduleIterator;
        }

        public ValueTask<T> Dequeue()
        {
            if (TryDequeue(out T i))
                return new ValueTask<T>(i);

            var waiter = new Waiter();

            lock (m_queue)
            {
                if (IsDisposed)
                {
                    FailWaiter(waiter);
                }
                else if (m_queue.Count > 0)
                {
                    T value = m_queue.Dequeue();
                    waiter.SetDequeueResult(value);
                }
                else if (IsAddingCompleted)
                {
                    FailWaiter(waiter);
                }
                else
                {
                    m_waiters.Enqueue(waiter);
                }
            }

            return new ValueTask<T>(waiter.Task);
        }

        public bool TryDequeue(out T item)
        {
            CheckIfIterating(false);

            lock (m_queue)
            {
                if (m_queue.Count > 0 && !IsDisposed)
                {
                    item = m_queue.Dequeue();
                    return true;
                }
            }

            item = default;
            return false;
        }

        public Task ForEachAsync(Action<T> action)
        {
            CheckIfIterating(beginIterating: true);
            m_iteratorActionCallback = action;
            return ForEachAsyncImpl();
        }

        public Task ForEachAsync(Func<T, Task> asyncAction)
        {
            CheckIfIterating(beginIterating: true);
            m_iteratorTaskCallback = asyncAction;
            return ForEachAsyncImpl();
        }

        public Task ForEachAsync(Func<T, ValueTask> asyncAction)
        {
            CheckIfIterating(beginIterating: true);
            m_iteratorValueTaskCallback = asyncAction;
            return ForEachAsyncImpl();
        }

        private Task ForEachAsyncImpl()
        {
            return m_iteratorCompletionTask.Task;
        }

        private static void TryDisposeItem(T item, Exception ex = null)
        {
            if (!(item is IDisposable disposable))
                return;

            try
            {
                if (ex != null && item is IAbortableItem abort)
                    abort.Abort(ex);
                else
                    disposable.Dispose();
            }
            catch
            {
            }
        }

        public Task CompleteAdding(Exception exception = null)
        {
            List<Waiter> waiters;

            lock (m_queue)
            {
                if (IsAddingCompleted)
                    return m_iteratorCompletionTask?.Task ?? Task.CompletedTask;

                IsAddingCompleted = true;
                m_disposeException = exception;

                waiters = m_waiters.ToList();
                m_waiters.Clear();

                if (m_isIteratorMode && !m_isInIteratorCallback)
                {
                    m_iteratorCompletionTask.TrySetResult(VoidType.Value);
                }
            }

            foreach (Waiter waiter in waiters)
            {
                FailWaiter(waiter);
            }

            return m_iteratorCompletionTask?.Task ?? Task.CompletedTask;
        }

        public void Dispose()
        {
            Dispose(m_disposeException);
        }

        public void Dispose(Exception exception)
        {
            List<T> items;
            List<Waiter> waiters;
            lock (m_queue)
            {
                if (IsDisposed)
                    return;

                IsAddingCompleted = true;
                IsDisposed = true;
                m_disposeException = exception;

                items = m_queue.ToList();
                waiters = m_waiters.ToList();

                if (m_isIteratorMode)
                {
                    if (m_isInIteratorCallback)
                    {
                        m_iteratorCompletionTask.TrySetCanceled();
                    }
                    else
                    {
                        m_iteratorCompletionTask.TrySetResult(VoidType.Value);
                    }
                }
            }

            if (DisposeIgnoredItems)
            {
                foreach (T item in items)
                    TryDisposeItem(item, exception);
            }

            foreach (Waiter waiter in waiters)
            {
                FailWaiter(waiter);
            }
        }

        private void FailWaiter(Waiter waiter)
        {
            if (m_isIteratorMode || m_disposeException is null)
                waiter.TrySetCanceled();
            else
                waiter.TrySetException(m_disposeException);
        }

        private void CheckIfIterating(bool beginIterating)
        {
            if (m_isIteratorMode)
                throw new InvalidOperationException("This instance is already in a ForEach iteration");

            if (!beginIterating)
                return;

            m_iteratorCompletionTask = new TaskCompletionSource<VoidType>(TaskCreationOptions.RunContinuationsAsynchronously);
            m_isIteratorMode = true;
            m_iteratorExecutionCompletedHandler = OnIteratorCallbackCompleted;
            m_iteratorThreadpoolInvoker = FastThreadpoolInvoker.Create(new AsyncQueueRescheduleItem { Parent = this });

            bool launchIterator = false;
            lock (m_queue)
            {
                if (m_queue.Count > 0)
                {
                    launchIterator = true;
                    m_isInIteratorCallback = true;
                }
            }

            if (launchIterator)
                RescheduleIterator();
        }

        private struct AsyncQueueRescheduleItem : IThreadPoolWorkItem
        {
            internal AsyncQueue<T> Parent;
            void IThreadPoolWorkItem.Execute() => Parent.RunIterator();
        }
    }

    internal interface IAbortableItem : IDisposable
    {
        void Abort(Exception ex);
    }
}

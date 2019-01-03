using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Utilities
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

            public void CancelDequeue(Exception exception)
            {
                if (exception == null)
                    TrySetCanceled();
                else
                    TrySetException(exception);
            }
        }

        private readonly Queue<T> m_queue;
        private readonly Queue<Waiter> m_waiters;
        private Exception m_disposeException;

        public bool IsAddingCompleted { get; private set; }
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// If set to 'True', items that are not raised after disposing the batcher
        /// will be disposed if they implement IDisposable.
        /// </summary>
        public bool DisposeIgnoredItems { get; set; }

        public int Count
        {
            get
            {
                lock (m_queue)
                {
                    return m_queue.Count;
                }
            }
        }

        public AsyncQueue()
        {
            m_queue = new Queue<T>();
            m_waiters = new Queue<Waiter>();
        }

        public void Enqueue(T item)
        {
            Waiter completedWaiter = null;
            bool isAddingCompleted = false;

            lock (m_queue)
            {
                if (IsAddingCompleted)
                {
                    isAddingCompleted = true;
                }
                else
                {
                    if (m_waiters.Count > 0)
                    {
                        completedWaiter = m_waiters.Dequeue();
                    }
                    else
                    {
                        m_queue.Enqueue(item);
                    }
                }
            }

            if (isAddingCompleted && DisposeIgnoredItems)
                TryDisposeItem(item);

            if (completedWaiter != null)
            {
                completedWaiter.SetDequeueResult(item); // we marked it as RunContinuationsAsynchronously, no need to possibly double-queue on threadpool
            }
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
                    waiter.CancelDequeue(m_disposeException);
                }
                else if (m_queue.Count > 0)
                {
                    T value = m_queue.Dequeue();
                    waiter.SetDequeueResult(value);
                }
                else if (IsAddingCompleted)
                {
                    waiter.CancelDequeue(m_disposeException);
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

        /// <summary>
        /// Execute the specified action for each item queued until the AsyncQueue is disposed.
        /// </summary>
        public async Task ForEachAsync(Action<T> action)
        {
            while (true)
            {
                T result;
                try
                {
                    result = await Dequeue().ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                action(result);
            }
        }

        /// <summary>
        /// Execute the specified action for each item queued until the AsyncQueue is disposed.
        /// </summary>
        public async Task ForEachAsync(Func<T, Task> asyncAction)
        {
            while (true)
            {
                T result;
                try
                {
                    result = await Dequeue().ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                await asyncAction(result).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Execute the specified action for each item queued until the AsyncQueue is disposed.
        /// </summary>
        public async Task ForEachAsync(Func<T, ValueTask> asyncAction)
        {
            while (true)
            {
                T result;
                try
                {
                    result = await Dequeue().ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                await asyncAction(result).ConfigureAwait(false);
            }
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

        public void CompleteAdding(Exception exception = null)
        {
            List<Waiter> waiters;

            lock (m_queue)
            {
                if (IsAddingCompleted)
                    return;

                IsAddingCompleted = true;
                m_disposeException = exception;

                waiters = m_waiters.ToList();
                m_waiters.Clear();
            }

            foreach (Waiter waiter in waiters)
                waiter.CancelDequeue(exception);
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
            }

            if (DisposeIgnoredItems)
            {
                foreach (T item in items)
                    TryDisposeItem(item, exception);
            }

            foreach (Waiter waiter in waiters)
                waiter.CancelDequeue(exception);
        }
    }

    internal interface IAbortableItem : IDisposable
    {
        void Abort(Exception ex);
    }
}

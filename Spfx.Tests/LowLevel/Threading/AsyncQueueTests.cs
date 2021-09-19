using NUnit.Framework;
using Spfx.Utilities.Threading;
using FluentAssertions;
using System.Threading.Tasks;
using Spfx.Utilities;

namespace Spfx.Tests.LowLevel.Threading
{
    [TestFixture, Parallelizable(ParallelScope.All)]
    public class AsyncQueueTests : CommonThreadingTestClass
    {
        public enum QueueMode
        {
            Manual,
            SyncCallback,
            AsyncCallback,
            SyncAsyncCallback
        }

        private static QueueMode[] AllModes = { QueueMode.Manual, QueueMode.AsyncCallback, QueueMode.SyncCallback, QueueMode.SyncAsyncCallback };

        [Test]
        [TestCaseSource(nameof(AllModes))]
        public async Task AsyncQueue_Manual_Basic(QueueMode mode)
        {
            var q = new WrappedQueue<object>(mode);
            q.IsDisposed.Should().BeFalse();

            using (q)
            {
                await BasicCheck(q);
            }

            q.IsAddingCompleted.Should().BeTrue();
            q.IsDisposed.Should().BeTrue();
        }

        [Test]
        [TestCaseSource(nameof(AllModes))]
        public async Task AsyncQueue_RemainingItemIsDisposed(QueueMode mode)
        {
            var objectNotDisposed = new DisposableObject();
            var objectNotDisposed2 = new DisposableObject();

            using (var nonDisposingQueue = new WrappedQueue<object>(mode))
            {
                nonDisposingQueue.Enqueue(objectNotDisposed);
            }

            var objectDisposed = new DisposableObject();

            using (var disposingQueue = new WrappedQueue<object>(mode))
            {
                disposingQueue.DisposeIgnoredItems = true;

                disposingQueue.Enqueue(objectNotDisposed2);
                disposingQueue.Enqueue(objectDisposed);

                var dequeued = await disposingQueue.TryDequeue(expectSuccess: true);
                dequeued.Should().BeSameAs(objectNotDisposed2);                
            }

            await WaitForAsync(() => objectDisposed.IsDisposed, OperationsTimeout);
            objectNotDisposed.IsDisposed.Should().BeFalse();
            objectNotDisposed2.IsDisposed.Should().BeFalse();
        }

        [Test]
        [TestCaseSource(nameof(AllModes))]
        public async Task AsyncQueue_Manual_Disposed(QueueMode mode)
        {
            var q = new WrappedQueue<object>(mode);
            q.IsDisposed.Should().BeFalse();

            Task ongoingDequeue;

            using (q)
            {
                await BasicCheck(q);
                ongoingDequeue = q.DequeueAsync().AsTask();
            }

            (await ongoingDequeue.TryWaitAsync(OperationsTimeout)).Should().BeTrue();

            ongoingDequeue.IsCompletedSuccessfully().Should().BeFalse();
            //AssertThrows<ObjectDisposedException>(() => ongoingDequeue.WaitOrRethrow());

            var dequeueTask = q.DequeueAsync();
            dequeueTask.IsCompleted.Should().BeTrue();
            dequeueTask.IsCompletedSuccessfully.Should().BeFalse();
            //AssertThrows<ObjectDisposedException>(() => _ = dequeueTask.Result);
        }

        private async Task BasicCheck(WrappedQueue<object> q)
        {
            q.IsAddingCompleted.Should().BeFalse();
            var dequeued = await q.TryDequeue(expectSuccess: false);
            dequeued.Should().BeNull();

            var obj = new object();
            q.Enqueue(obj);

            dequeued = await q.TryDequeue(expectSuccess: true);
            dequeued.Should().BeSameAs(obj);
        }

        private class WrappedQueue<T> : Disposable
        {
            private readonly AsyncQueue<T> m_enqueuingQueue;
            private readonly AsyncQueue<T> m_dequeuingQueue;

            public WrappedQueue(QueueMode mode)
            {
                m_enqueuingQueue = new AsyncQueue<T>();

                if (mode == QueueMode.Manual)
                {
                    m_dequeuingQueue = m_enqueuingQueue;
                }
                else
                {
                    m_dequeuingQueue = new AsyncQueue<T>();
                    if (mode == QueueMode.SyncCallback)
                    {
                        m_enqueuingQueue.ForEachAsync(i => m_dequeuingQueue.Enqueue(i));
                    }
                    else if (mode == QueueMode.AsyncCallback)
                    {
                        m_enqueuingQueue.ForEachAsync(i => { m_dequeuingQueue.Enqueue(i); return Task.Delay(1); });
                    }
                    else if (mode == QueueMode.SyncAsyncCallback)
                    {
                        m_enqueuingQueue.ForEachAsync(i => { m_dequeuingQueue.Enqueue(i); return Task.CompletedTask; });
                    }
                }
            }

            public bool IsAddingCompleted => m_enqueuingQueue.IsAddingCompleted;

            public bool DisposeIgnoredItems
            {
                set
                {
                    m_enqueuingQueue.DisposeIgnoredItems = value;
                    m_dequeuingQueue.DisposeIgnoredItems = value;
                }
            }

            protected override void OnDispose()
            {
                m_enqueuingQueue.Dispose();
                m_dequeuingQueue.Dispose();
                base.OnDispose();
            }

            internal void Enqueue(T obj)
            {
                m_enqueuingQueue.Enqueue(obj);
            }

            internal async Task<T> TryDequeue(bool expectSuccess)
            {
                T val = default;

                if (expectSuccess)
                {
                    await WaitForAsync(() => m_dequeuingQueue.TryDequeue(out val), OperationsTimeout);
                    return val;
                }

                m_dequeuingQueue.TryDequeue(out val).Should().BeFalse();
                return val;
            }

            internal ValueTask<T> DequeueAsync()
            {
                return m_dequeuingQueue.DequeueAsync();
            }
        }
    }
}

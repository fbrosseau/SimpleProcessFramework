using FluentAssertions;
using NUnit.Framework;
using Spfx.Utilities.Threading;
using System;
using System.Threading.Tasks;

namespace Spfx.Tests.LowLevel.Threading
{
    [TestFixture, Parallelizable(ParallelScope.All)]
    public class AsyncLockTests : CommonThreadingTestClass
    {
        [Test]
        public void AsyncLock_Basic()
        {
            var theLock = new AsyncLock();
            theLock.IsDisposed.Should().BeFalse();

            using (theLock)
            {
                BasicCheck(theLock);
            }

            theLock.IsDisposed.Should().BeTrue();
        }

        [Test]
        public void AsyncLock_Disposed()
        {
            var theLock = new AsyncLock();
            theLock.IsDisposed.Should().BeFalse();

            theLock.Dispose();
            theLock.IsDisposed.Should().BeTrue();

            AssertThrows<ObjectDisposedException>(() => theLock.LockAsync().AsTask().GetResultOrRethrow());
        }

        [Test]
        public async Task AsyncLock_Concurrent()
        {
            using var theLock = new AsyncLock();

            theLock.IsLockTaken.Should().BeFalse();

            var sessionTask = theLock.LockAsync();
            sessionTask.IsCompleted.Should().BeTrue();
            theLock.IsLockTaken.Should().BeTrue();
            var session = sessionTask.Result;

            var sessionTask2 = theLock.LockAsync().AsTask();
            sessionTask2.IsCompleted.Should().BeFalse();

            var sessionTask3 = theLock.LockAsync().AsTask();
            sessionTask3.IsCompleted.Should().BeFalse();

            session.Dispose();
            theLock.IsLockTaken.Should().BeTrue();
            var session2 = await sessionTask2.WT();
            theLock.IsLockTaken.Should().BeTrue();

            session2.Dispose();
            theLock.IsLockTaken.Should().BeTrue();
            var session3 = await sessionTask3.WT();
            theLock.IsLockTaken.Should().BeTrue();

            session3.Dispose();

            BasicCheck(theLock);
        }

        private void BasicCheck(AsyncLock theLock)
        {
            theLock.IsLockTaken.Should().BeFalse();

            var sessionTask = theLock.LockAsync();
            sessionTask.IsCompletedSuccessfully.Should().BeTrue();
            using (var session = sessionTask.Result)
            {
                theLock.IsLockTaken.Should().BeTrue();
            }

            theLock.IsLockTaken.Should().BeFalse();
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IAsyncDestroyable : IDisposable
    {
        bool HasAsyncTeardownStarted { get; }
        bool HasTeardownStarted { get; }

        ValueTask DisposeAsync();
        Task TeardownAsync(CancellationToken ct = default);
    }

    public static class AsyncDestroyableExtensions
    {
        public static async Task TeardownAsync(this IAsyncDestroyable d, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            await d.TeardownAsync(cts.Token).ConfigureAwait(false);
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public class AsyncDestroyable : Disposable, IAsyncDestroyable, IAsyncDisposable
    {
        public bool HasAsyncTeardownStarted => m_teardownTask != null;
        public sealed override bool HasTeardownStarted => HasDisposeStarted || HasAsyncTeardownStarted;

        private Task m_teardownTask;
        private TaskCompletionSource<VoidType> m_teardownTaskCompletion;

        protected override void OnDispose()
        {
            base.OnDispose();
            if (m_teardownTask is null)
                Interlocked.CompareExchange(ref m_teardownTask, Task.CompletedTask, null);

            m_teardownTaskCompletion?.TryComplete();
        }

        public Task TeardownAsync(CancellationToken ct = default)
        {
            if (HasTeardownStarted)
                return EnsureHasShutdownTask();

            lock (DisposeLock)
            {
                if (!HasTeardownStarted)
                {
                    try
                    {
                        m_teardownTask = OnTeardownAsync(ct).AsTask();
                    }
                    catch (Exception ex)
                    {
                        m_teardownTask = Task.FromException(ex);
                    }
                }
            }

            return EnsureHasShutdownTask();
        }

        private Task EnsureHasShutdownTask()
        {
            if (m_teardownTask != null)
                return m_teardownTask;

            var tcs = new TaskCompletionSource<VoidType>();
            Interlocked.CompareExchange(ref m_teardownTaskCompletion, tcs, null);
            Interlocked.CompareExchange(ref m_teardownTask, m_teardownTaskCompletion.Task, null);
            return m_teardownTask;
        }

        protected override void ThrowIfDisposing()
        {
            base.ThrowIfDisposing();
            if (HasAsyncTeardownStarted)
                throw new ObjectDisposedException(GetType().FullName);
        }

        protected virtual ValueTask OnTeardownAsync(CancellationToken ct = default)
        {
            return default;
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(TeardownAsync());
        }

        public static async ValueTask TeardownAll(IEnumerable<IAsyncDestroyable> instances, CancellationToken ct = default)
        {
            var teardownTasks = new List<Task>();

            foreach (var i in instances)
            {
                teardownTasks.Add(i.TeardownAsync(ct));
            }

            await Task.WhenAll(teardownTasks).ConfigureAwait(false);
        }
    }
}
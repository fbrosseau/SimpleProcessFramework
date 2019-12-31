using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
    internal static partial class TaskEx
    {
        public static Task<Task> Wrap(this Task t)
        {
            return t.ContinueWith(innerT => innerT, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
        public static Task<Task<T>> Wrap<T>(this Task<T> t)
        {
            return t.ContinueWith(innerT => innerT, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public static Task WithCancellation(this Task t, CancellationToken ct)
        {
            return t.ContinueWith(inner => inner, ct, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap();
        }

        public static Task<T> WithCancellation<T>(this Task<T> t, CancellationToken ct)
        {
            return t.ContinueWith(inner => inner, ct, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap();
        }

        public static void SafeCancel(this CancellationTokenSource cts)
        {
            try
            {
                cts.Cancel();
            }
            catch
            {
                // objectdisposedexception etc
            }
        }

        public static void SafeCancelAsync(this CancellationTokenSource cts)
        {
            ThreadPool.QueueUserWorkItem(s => ((CancellationTokenSource)s).SafeCancel(), cts);
        }

        public static void CompleteWith<T>(this TaskCompletionSource<T> tcs, Task task)
        {
            task.ContinueWith((innerTask, s) =>
            {
                var innerTcs = (TaskCompletionSource<T>)s;
                if (innerTask.Status == TaskStatus.RanToCompletion)
                {
                    if (typeof(T) == typeof(VoidType))
                    {
                        innerTcs.TrySetResult((T)VoidType.BoxedValue);
                    }
                    else
                    {
                        innerTcs.TrySetResult(((Task<T>)task).Result);
                    }
                }
                else if (innerTask.Status == TaskStatus.Canceled)
                {
                    innerTcs.TrySetCanceled();
                }
                else if (innerTask.Status == TaskStatus.Faulted)
                {
                    innerTcs.TrySetException(innerTask.Exception);
                }
            }, tcs, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public static void CompleteWithResultAsObject<T>(this TaskCompletionSource<object> tcs, Task task)
        {
            Guard.ArgumentNotNull(task, nameof(task));

            task.ContinueWith((innerTask, s) =>
            {
                var innerTcs = (TaskCompletionSource<object>)s;
                if (innerTask.Status == TaskStatus.RanToCompletion)
                {
                    if (typeof(T) == typeof(VoidType))
                    {
                        innerTcs.TrySetResult(null);
                    }
                    else
                    {
                        innerTcs.TrySetResult(BoxHelper.Box(((Task<T>)innerTask).Result));
                    }
                }
                else if (innerTask.Status == TaskStatus.Canceled)
                {
                    innerTcs.TrySetCanceled();
                }
                else if (innerTask.Status == TaskStatus.Faulted)
                {
                    innerTcs.TrySetException(innerTask.Exception);
                }
            }, tcs, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        internal static Task<TOut> ContinueWithCast<TIn, TOut>(Task<TIn> t)
        {
            if (t is Task<TOut> taskOfT)
                return taskOfT;

            if (t.IsCompleted)
            {
                return TaskCache.FromResult((TOut)(object)t.Result);
            }

            return t.ContinueWith(innerT =>
            {
                return (TOut)(object)innerT.Result;
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        internal static async ValueTask ExpectFirstTask(Task taskExpectedToComplete, Task taskNotExpectedToWin)
        {
            var winner = await Task.WhenAny(taskExpectedToComplete, taskNotExpectedToWin).ConfigureAwait(false);
            await winner.ConfigureAwait(false);
            if (ReferenceEquals(winner, taskExpectedToComplete))
                return;

            throw new TaskCanceledException("The expected task did not complete first");
        }

        public static bool IsFaultedOrCanceled(this Task t)
        {
            Guard.ArgumentNotNull(t, nameof(t));
            return t.IsFaulted || t.IsCanceled;
        }

        public static bool IsFaultedOrCanceled(this ValueTask t)
        {
            return t.IsFaulted || t.IsCanceled;
        }

        public static bool IsFaultedOrCanceled<T>(this ValueTask<T> t)
        {
            return t.IsFaulted || t.IsCanceled;
        }

        public static Exception GetExceptionOrCancel(this Task t)
        {
            Guard.ArgumentNotNull(t, nameof(t));
            if (t.IsFaulted)
                return t.ExtractException();
            if (t.IsCanceled)
                return new TaskCanceledException(t);

            throw new InvalidOperationException("This task is not fauled or canceled");
        }

        public static Exception GetExceptionOrCancel(this ValueTask t)
        {
            if (t.IsFaulted)
                return t.ExtractException();
            if (t.IsCanceled)
                return new TaskCanceledException(t.AsTask());

            throw new InvalidOperationException("This task is not fauled or canceled");
        }

        public static Exception GetExceptionOrCancel<T>(this ValueTask<T> t)
        {
            if (t.IsFaulted)
                return t.ExtractException();
            if (t.IsCanceled)
                return new TaskCanceledException(t.AsTask());

            throw new InvalidOperationException("This task is not fauled or canceled");
        }

        public static void RethrowException(this Task t)
        {
            Debug.Assert(t.IsFaultedOrCanceled());
            t.GetExceptionOrCancel().Rethrow();
        }

        public static void RethrowException(this ValueTask t)
        {
            Debug.Assert(t.IsFaultedOrCanceled());
            t.GetExceptionOrCancel().Rethrow();
        }

        public static void RethrowException<T>(this ValueTask<T> t)
        {
            t.GetExceptionOrCancel().Rethrow();
        }

        public static void FireAndForget(this Task t)
        {
            // empty on purpose
        }

        public static Task<bool> WaitAsync(this Task t, TimeSpan timeout)
        {
            if (t.IsCompleted)
                return TaskCache.TrueTask;

            if (timeout == TimeSpan.Zero)
                return TaskCache.FalseTask;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            t.ContinueWith((innerT, s) =>
            {
                var innerTcs = (TaskCompletionSource<bool>)s;
                innerTcs.TrySetResult(true);
            }, tcs, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            Task.Delay(timeout).ContinueWith((innerT, s) =>
            {
                var innerTcs = (TaskCompletionSource<bool>)s;
                innerTcs.TrySetResult(false);
            }, tcs, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            return tcs.Task;
        }

        [DebuggerStepThrough, DebuggerHidden]
        public static T WaitOrTimeout<T>(this Task<T> t, TimeSpan timeout)
        {
            ((Task)t).WaitOrTimeout(timeout);
            return t.Result;
        }

        [DebuggerStepThrough, DebuggerHidden]
        public static void WaitOrTimeout(this Task t, TimeSpan timeout)
        {
            if (!t.Wait(timeout))
                throw new TimeoutException();
        }

        [DebuggerStepThrough, DebuggerHidden]
        public static void WaitOrRethrow(this Task t)
        {
            t.GetAwaiter().GetResult();
        }

        [DebuggerStepThrough, DebuggerHidden]
        public static bool WaitSilent(this Task t, int timeoutMilliseconds)
        {
            if (t.IsCompleted)
                return true;

            return t.Wrap().Wait(timeoutMilliseconds);
        }

        [DebuggerStepThrough, DebuggerHidden]
        public static bool WaitSilent(this Task t, TimeSpan timeout)
        {
            return WaitSilent(t, (int)timeout.TotalMilliseconds);
        }

        public static void ExpectAlreadyCompleted(this Task t)
        {
            if (!t.IsCompleted)
                throw new InvalidOperationException("This task was supposed to be already completed");
        }

        public static Exception ExtractException(this Task t)
        {
            Exception ex = t.Exception;
            while (ex is AggregateException && ex.InnerException != null)
                ex = ex.InnerException;
            return ex;
        }

        public static Exception ExtractException(this ValueTask t)
        {
            return t.AsTask().ExtractException();
        }

        public static Exception ExtractException<T>(this ValueTask<T> t)
        {
            return t.AsTask().ExtractException();
        }

        public static Task WhenAllOrRethrow(Task t1) => t1;
        public static Task WhenAllOrRethrow(Task t1, Task t2) => WhenAllOrRethrowInternal(new List<Task> { t1, t2 });
        public static Task WhenAllOrRethrow(Task t1, Task t2, Task t3) => WhenAllOrRethrowInternal(new List<Task> { t1, t2, t3 });
        public static Task WhenAllOrRethrow(IEnumerable<Task> tasks) => WhenAllOrRethrowInternal(tasks.ToList());

        private static async Task WhenAllOrRethrowInternal(List<Task> remaining)
        {
            while (remaining.Count > 0)
            {
                if (remaining.Count == 1)
                {
                    await remaining[0];
                    return;
                }

                for (int i = remaining.Count - 1; i >= 0; --i)
                {
                    if (!remaining[i].IsCompleted)
                        continue;

                    remaining[i].Wait();
                    if (remaining.Count == 1)
                        return;
                    remaining.RemoveAt(i);
                }

                if (remaining.Count == 1)
                {
                    await remaining[0];
                    return;
                }

                await Task.WhenAny(remaining).ConfigureAwait(false);
            }
        }

        public static Task<VoidType> ToVoidTypeTask(Task t)
        {
            if (t.Status == TaskStatus.RanToCompletion)
                return TaskCache.VoidTypeTask;

            return t.ContinueWith(t =>
            {
                t.WaitOrRethrow();
                return VoidType.Value;
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }

        public static Task<TResult> ToApm<TResult>(Task<TResult> task, AsyncCallback callback, object state)
        {
            if (task.AsyncState == state)
            {
                if (callback != null)
                {
                    task.ContinueWith(delegate { callback(task); },
                        CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
                }
                return task;
            }

            var tcs = new TaskCompletionSource<TResult>(state);
            task.ContinueWith(delegate
            {
                if (task.IsFaulted) tcs.TrySetException(task.Exception.InnerExceptions);
                else if (task.IsCanceled) tcs.TrySetCanceled();
                else tcs.TrySetResult(task.Result);

                if (callback != null) callback(tcs.Task);

            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            return tcs.Task;
        }

        public static TResult EndApm<TResult>(IAsyncResult ar)
        {
            return ((Task<TResult>)ar).Result;
        }
    }
}
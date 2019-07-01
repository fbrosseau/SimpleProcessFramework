using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities.Threading
{
    internal static class TaskEx
    {
        public static Task NeverCompletingTask { get; } = new TaskCompletionSource<VoidType>().Task;

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

        internal static async ValueTask ExpectFirstTask(Task taskExpectedToComplete, Task taskNotExpectedToWin)
        {
            var winner = await Task.WhenAny(taskExpectedToComplete, taskNotExpectedToWin).ConfigureAwait(false);
            await winner.ConfigureAwait(false);
            if (ReferenceEquals(winner, taskExpectedToComplete))
                return;

            throw new TaskCanceledException("The expected task did not complete first");
        }

        public static void FireAndForget(this Task t)
        {
            // empty on purpose
        }

        public static Task<bool> WaitAsync(this Task t, TimeSpan timeout)
        {
            if (t.IsCompleted)
                return Task.FromResult(true);

            if (timeout == TimeSpan.Zero)
                return Task.FromResult(false);

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

        public static T WaitOrTimeout<T>(this Task<T> t, TimeSpan timeout)
        {
            ((Task)t).WaitOrTimeout(timeout);
            return t.Result;
        }

        public static void WaitOrTimeout(this Task t, TimeSpan timeout)
        {
            if (!t.Wait(timeout))
                throw new TimeoutException();
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
    }
}
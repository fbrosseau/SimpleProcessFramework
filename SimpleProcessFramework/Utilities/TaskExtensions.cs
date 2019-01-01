using Oopi.Utilities;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Utilities
{
    internal static class TaskExtensions
    {
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
    }
}
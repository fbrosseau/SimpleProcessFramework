using System.Threading;

namespace Spfx.Utilities.Threading
{
#if !NETCOREAPP3_0_PLUS
    internal interface IThreadPoolWorkItem
    {
        void Execute();
    }

#endif

    internal static class ThreadPoolHelper
    {
        public static void QueueItem(IThreadPoolWorkItem item)
        {
#if !NETCOREAPP3_0_PLUS
            ThreadPool.UnsafeQueueUserWorkItem(s => ((IThreadPoolWorkItem)s).Execute(), item);
#else
            ThreadPool.UnsafeQueueUserWorkItem(item, false);
#endif
        }
    }
}
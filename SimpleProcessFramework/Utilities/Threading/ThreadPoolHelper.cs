using System.Threading;

namespace Spfx.Utilities.Threading
{
    internal interface IThreadPoolItem
    {
        void Execute();
    }

    internal static class ThreadPoolHelper
    {
        public static void QueueItem(IThreadPoolItem item)
        {
            ThreadPool.UnsafeQueueUserWorkItem(s => ((IThreadPoolItem)s).Execute(), item);
        }
    }
}

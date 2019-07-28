using System.Threading;

#if !NETCOREAPP3_0_PLUS
namespace System.Threading
{
    internal interface IThreadPoolWorkItem
    {
        void Execute();
    }
}
#endif

namespace Spfx.Utilities.Threading
{
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
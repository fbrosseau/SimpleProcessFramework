using System;
using System.Threading;

namespace Spfx.Utilities.Threading
{
    public static class FastThreadpoolInvoker
    {
        internal static FastThreadpoolInvoker<TCallbackInvoker> Create<TCallbackInvoker>(TCallbackInvoker invoker)
            where TCallbackInvoker : IThreadPoolWorkItem
        {
            return new FastThreadpoolInvoker<TCallbackInvoker>(invoker);
        }
    }

    internal struct FastThreadpoolInvoker<TCallbackInvoker>
        where TCallbackInvoker : IThreadPoolWorkItem
    {
        private readonly IThreadPoolWorkItem m_realCallback;

        public FastThreadpoolInvoker(TCallbackInvoker invoker)
        {
            if (invoker == null) throw new ArgumentNullException(nameof(invoker));
            m_realCallback = invoker;
        }

        public void Invoke()
        {
            if (m_realCallback is null)
                throw new InvalidOperationException("This instance was not initialized");

#if !NETCOREAPP3_0_PLUS
            ThreadPool.UnsafeQueueUserWorkItem(s_callback, m_realCallback);
#else
            ThreadPool.UnsafeQueueUserWorkItem(m_realCallback, preferLocal: false);
#endif
        }

#if !NETCOREAPP3_0_PLUS
        private static readonly WaitCallback s_callback = OnCallback;

        private static void OnCallback(object state)
        {
            ((TCallbackInvoker)state).Execute();
        }
#endif
    }
}

using System;
using System.Threading;
using System.Threading.Tasks.Sources;

namespace Spfx.Utilities.Threading
{
    internal static class ThreadPoolInvoker
    {
        internal static ThreadPoolInvoker<TCallbackInvoker> Create<TCallbackInvoker>(TCallbackInvoker invoker)
            where TCallbackInvoker : IThreadPoolWorkItem
        {
            return new ThreadPoolInvoker<TCallbackInvoker>(invoker);
        }
    }

    internal struct ThreadPoolInvoker<TCallbackInvoker>
        where TCallbackInvoker : IThreadPoolWorkItem
    {
        private readonly TCallbackInvoker m_realCallback;
        private IThreadPoolWorkItem m_boxedCallback;

        private static readonly IThreadPoolWorkItem s_uninitializedSentinel = new DummyWorkItem();

        private class DummyWorkItem : IThreadPoolWorkItem
        {
            public void Execute() => BadCodeAssert.ThrowInvalidOperation(nameof(DummyWorkItem));
        }

        public ThreadPoolInvoker(TCallbackInvoker invoker, IThreadPoolWorkItem boxedCallback = null)
        {
            if (invoker == null) throw new ArgumentNullException(nameof(invoker));
            m_realCallback = invoker;
            m_boxedCallback = boxedCallback ?? s_uninitializedSentinel;
        }

        private IThreadPoolWorkItem GetBoxedCallback()
        {
            if (m_realCallback is null)
                BadCodeAssert.ThrowInvalidOperation("This instance was not initialized");
            if (ReferenceEquals(s_uninitializedSentinel, m_boxedCallback))
                m_boxedCallback = m_realCallback;
            return m_boxedCallback;
        }

        internal void InvokeValueTaskCompletion(ValueTaskSourceOnCompletedFlags flags)
        {
            if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
            {
                Invoke();
            }
            else
            {
                UnsafeInvoke();
            }
        }

        public void UnsafeInvoke()
        {
#if !NETCOREAPP3_0_PLUS
            ThreadPool.UnsafeQueueUserWorkItem(s_waitCallback, GetBoxedCallback());
#else
            ThreadPool.UnsafeQueueUserWorkItem(GetBoxedCallback(), preferLocal: false);
#endif
        }

        internal void Invoke()
        {
#if !NETCOREAPP3_0_PLUS
            ThreadPool.QueueUserWorkItem(s_waitCallback, GetBoxedCallback());
#else
            ThreadPool.QueueUserWorkItem(s_statefulCallback, m_realCallback, preferLocal: false);
#endif
        }

#if NETCOREAPP3_0_PLUS
        private static readonly Action<TCallbackInvoker> s_statefulCallback = StatefulCallback;
        private static void StatefulCallback(TCallbackInvoker state)
        {
            state.Execute();
        }
#endif

#if !NETCOREAPP3_0_PLUS
        private static readonly WaitCallback s_waitCallback = OnCallback;

        private static void OnCallback(object state)
        {
            ((TCallbackInvoker)state).Execute();
        }
#endif
    }
}

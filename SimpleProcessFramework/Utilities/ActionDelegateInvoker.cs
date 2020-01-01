using System;
using System.Runtime.CompilerServices;

namespace Spfx.Utilities
{
    internal interface IDelegateInvoker
    {
        void Invoke();
    }

    internal readonly struct ActionDelegateInvoker : IDelegateInvoker
    {
        private readonly Action m_func;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ActionDelegateInvoker(Action a)
        {
            Guard.ArgumentNotNull(a, nameof(a));
            m_func = a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke()
        {
            m_func();
        }
    }

    internal readonly struct ActionDelegateInvoker<T1> : IDelegateInvoker
    {
        private readonly Action<T1> m_func;
        private readonly T1 m_a1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ActionDelegateInvoker(Action<T1> a, T1 arg1)
        {
            Guard.ArgumentNotNull(a, nameof(a));
            m_func = a;
            m_a1 = arg1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invoke()
        {
            m_func(m_a1);
        }
    }
}

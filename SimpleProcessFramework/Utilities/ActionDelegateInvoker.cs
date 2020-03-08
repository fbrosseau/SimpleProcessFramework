using System;
using System.Runtime.CompilerServices;

namespace Spfx.Utilities
{
    internal interface IDelegateInvoker
    {
        void Invoke();
    }

    internal interface IFuncBoxedInvoker
    {
        object Invoke();
    }

    internal interface IFuncInvoker<out TReturn> : IFuncBoxedInvoker
    {
        new TReturn Invoke();
    }

    internal readonly struct ActionDelegateInvoker : IDelegateInvoker
    {
        private readonly Action m_func;

        public static ActionDelegateInvoker Create(Action a) => new ActionDelegateInvoker(a);
        public static ActionDelegateInvoker<T1> Create<T1>(Action<T1> a, T1 arg1) => new ActionDelegateInvoker<T1>(a, arg1);

        public static FuncDelegateInvoker<TReturn> Create<TReturn>(Func<TReturn> a) => FuncDelegateInvoker<TReturn>.Create(a);
        public static FuncDelegateInvoker<T1, TReturn> Create<T1, TReturn>(Func<T1, TReturn> a, T1 arg1) => FuncDelegateInvoker<TReturn>.Create(a, arg1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ActionDelegateInvoker(Action a)
        {
            Guard.ArgumentNotNull(a, nameof(a));
            m_func = a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Invoke()
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
        public readonly void Invoke()
        {
            m_func(m_a1);
        }
    }

    internal readonly struct FuncDelegateInvoker<TReturn> : IFuncInvoker<TReturn>
    {
        private readonly Func<TReturn> m_func;

        public static FuncDelegateInvoker<TReturn> Create(Func<TReturn> a) => new FuncDelegateInvoker<TReturn>(a);
        public static FuncDelegateInvoker<T1, TReturn> Create<T1>(Func<T1, TReturn> a, T1 arg1) => new FuncDelegateInvoker<T1, TReturn>(a, arg1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FuncDelegateInvoker(Func<TReturn> a)
        {
            Guard.ArgumentNotNull(a, nameof(a));
            m_func = a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly TReturn Invoke()
        {
            return m_func();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly object IFuncBoxedInvoker.Invoke()
        {
            return BoxHelper.Box(Invoke());
        }
    }

    internal readonly struct FuncDelegateInvoker<T1, TReturn> : IFuncInvoker<TReturn>
    {
        private readonly Func<T1, TReturn> m_func;
        private readonly T1 m_arg1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FuncDelegateInvoker(Func<T1, TReturn> a, T1 arg1)
        {
            Guard.ArgumentNotNull(a, nameof(a));
            m_func = a;
            m_arg1 = arg1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly TReturn Invoke()
        {
            return m_func(m_arg1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly object IFuncBoxedInvoker.Invoke()
        {
            return BoxHelper.Box(Invoke());
        }
    }
}

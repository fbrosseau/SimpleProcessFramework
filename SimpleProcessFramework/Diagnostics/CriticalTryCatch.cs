using Spfx.Reflection;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Spfx.Diagnostics
{
    internal static class CriticalTryCatch
    {
        public static Action<Exception> UnhandledExceptionHandler { get; set; }

        private static ParameterizedThreadStart DefaultRunWithActionAsObjectAsThreadStart { get; } = DefaultRunWithActionAsObject;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DefaultRunWithActionAsObject(object o) => DefaultRun((Action)o);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DefaultRun(Action func) => Run(DefaultTypeResolverFactory.DefaultTypeResolver, func);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Run(ITypeResolver typeResolver, Action func) => Run(typeResolver, ActionDelegateInvoker.Create(func));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Run<TState>(ITypeResolver typeResolver, TState state, Action<TState> func) => Run(typeResolver, ActionDelegateInvoker.Create(func, state));

        public static void Run<TDelegateInvoker>(ITypeResolver typeResolver, in TDelegateInvoker delegateInvoker)
            where TDelegateInvoker : IDelegateInvoker
        {
            IUnhandledExceptionsHandler handler = null;
            IUnhandledExceptionsHandler GetHandler()
            {
                try
                {
                    if (handler is null)
                        handler = typeResolver.CreateSingleton<IUnhandledExceptionsHandler>();
                }
                catch
                {
                    handler = DefaultUnhandledExceptionHandler.Instance;
                }
                return handler;
            }

            try
            {
                delegateInvoker.Invoke();
            }
            catch (Exception ex) when (GetHandler().FilterCaughtException(ex))
            {
                GetHandler().HandleCaughtException(ex);
            }
        }

        public static Thread StartThread(string threadName, Action callback, ThreadPriority priority = ThreadPriority.Normal, bool suppressFlow = false)
        {
            using (TaskEx.MaybeSuppressExecutionContext(suppressFlow))
            {
                var thread = new Thread(DefaultRunWithActionAsObjectAsThreadStart)
                {
                    Name = threadName,
                    IsBackground = true
                };

                if (priority != ThreadPriority.Normal)
                {
                    thread.Priority = priority;
                }

                thread.Start(callback);
                return thread;
            }
        }
    }
}
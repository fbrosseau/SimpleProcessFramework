using Spfx.Reflection;
using Spfx.Utilities;
using System;
using System.Runtime.CompilerServices;

namespace Spfx.Diagnostics
{
    internal static class CriticalTryCatch
    {
        public static Action<Exception> UnhandledExceptionHandler { get; set; }

        public static void DefaultRunWithActionAsObject(object o)
        {
            DefaultRun((Action)o);
        }

        public static void DefaultRun(Action func)
        {
            Run(DefaultTypeResolverFactory.DefaultTypeResolver, func);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Run(ITypeResolver typeResolver, Action func) => Run(typeResolver, new ActionDelegateInvoker(func));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Run<TState>(ITypeResolver typeResolver, TState state, Action<TState> func) => Run(typeResolver, new ActionDelegateInvoker<TState>(func, state));

        public static void Run<TFunc>(ITypeResolver typeResolver, TFunc func)
            where TFunc : IDelegateInvoker
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
                func.Invoke();
            }
            catch (Exception ex) when (GetHandler().FilterCaughtException(ex))
            {
                GetHandler().HandleCaughtException(ex);
            }
        }
    }
}
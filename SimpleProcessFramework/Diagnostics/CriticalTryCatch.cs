using Spfx.Diagnostics.Logging;
using Spfx.Reflection;
using System;
using System.Diagnostics;

namespace Spfx.Diagnostics
{
    internal static class CriticalTryCatch
    {
        public static Action<Exception> UnhandledExceptionHandler { get; set; }

        public static void Run<TState>(ITypeResolver typeResolver, TState state, Action<TState> func)
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
                func(state);
            }
            catch (Exception ex) when (GetHandler().FilterCaughtException(ex))
            {
                GetHandler().HandleCaughtException(ex);
            }
        }
    }
}
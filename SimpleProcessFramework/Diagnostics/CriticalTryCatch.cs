using Spfx.Diagnostics.Logging;
using Spfx.Reflection;
using System;
using System.Diagnostics;

namespace Spfx.Diagnostics
{
    public interface IUnhandledExceptionsHandler
    {
        bool FilterCaughtException(Exception ex);
        void HandleCaughtException(Exception ex);
    }

    internal class DefaultUnhandledExceptionHandler : IUnhandledExceptionsHandler
    {
        private static IUnhandledExceptionsHandler s_instance;
        internal static IUnhandledExceptionsHandler Instance
        {
            get
            {
                if (s_instance is null)
                    s_instance = new DefaultUnhandledExceptionHandler(DefaultTypeResolverFactory.DefaultTypeResolver);
                return s_instance;
            }
        }

        private readonly ILogger m_logger;

        public DefaultUnhandledExceptionHandler(ITypeResolver typeResolver)
        {
            m_logger = typeResolver.GetLogger(typeof(DefaultUnhandledExceptionHandler), uniqueInstance: true);
        }

        public virtual bool FilterCaughtException(Exception ex)
        {
            if (Debugger.IsAttached)
                Debugger.Break();

            return true;
        }

        public virtual void HandleCaughtException(Exception ex)
        {
            m_logger.Warn?.Trace(ex, "Unhandled exception");
            CriticalTryCatch.UnhandledExceptionHandler?.Invoke(ex);
        }
    }

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
                    return handler;
                }
                catch
                {
                    return DefaultUnhandledExceptionHandler.Instance;
                }
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
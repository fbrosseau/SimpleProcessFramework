using Spfx.Diagnostics.Logging;
using Spfx.Reflection;
using Spfx.Utilities;
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
        public static bool DefaultBreakOnExceptions { get; set; } = HostFeaturesHelper.IsDebugBuild;

        public virtual bool BreakOnExceptions { get; } = DefaultBreakOnExceptions;

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
            if (Debugger.IsAttached && BreakOnExceptions)
                Debugger.Break();

            return true;
        }

        public virtual void HandleCaughtException(Exception ex)
        {
            m_logger.Warn?.Trace(ex, "Unhandled exception");
            CriticalTryCatch.UnhandledExceptionHandler?.Invoke(ex);
        }
    }
}
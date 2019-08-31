using Spfx.Reflection;
using System;
using System.ComponentModel;

namespace Spfx.Diagnostics.Logging
{
    [EditorBrowsable(EditorBrowsableState.Never)] // marking as Browsable-Never to avoid polluting people's intellisense with very common names
    public interface ILogger : IDisposable
    {
        string LoggerName { get; }

        ConfiguredLogger? Debug { get; }
        ConfiguredLogger? Info { get; }
        ConfiguredLogger? Warn { get; }
        ConfiguredLogger? Error { get; }
        ConfiguredLogger? FromLevel(LogTraceLevel level);

        void Trace(LogTraceLevel level, string message, Exception ex = null);
    }

    public static class LoggerExtensions
    {
        public static ILogger GetLogger(this ITypeResolver typeResolver, Type loggedType, bool uniqueInstance = false)
        {
            return typeResolver.CreateSingleton<ILoggerFactory>().GetLogger(loggedType, uniqueInstance);
        }
    }
}

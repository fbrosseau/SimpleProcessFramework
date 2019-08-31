using Spfx.Utilities;
using System;

namespace Spfx.Diagnostics.Logging
{
    internal class DefaultLogger : ILogger
    {
        private readonly ILogListener m_listener;

        public string LoggerName { get; }
        public ConfiguredLogger? Debug => FromLevel(LogTraceLevel.Debug);
        public ConfiguredLogger? Info => FromLevel(LogTraceLevel.Info);
        public ConfiguredLogger? Warn => FromLevel(LogTraceLevel.Warn);
        public ConfiguredLogger? Error => FromLevel(LogTraceLevel.Error);

        internal LogTraceLevel EnabledLevels { get; set; }

        public DefaultLogger(ILogListener listener, string loggerName)
        {
            Guard.ArgumentNotNull(listener, nameof(listener));
            Guard.ArgumentNotNullOrEmpty(loggerName, nameof(loggerName));

            m_listener = listener;
            LoggerName = loggerName;
        }

        public void Dispose()
        {
        }

        public ConfiguredLogger? FromLevel(LogTraceLevel level)
        {
            if (IsEnabled(level))
                return new ConfiguredLogger(this, level);
            return null;
        }

        private bool IsEnabled(LogTraceLevel level)
        {
            return (EnabledLevels & level) != 0;
        }

        public void Trace(LogTraceLevel level, string message, Exception ex = null)
        {
            if (IsEnabled(level))
                m_listener.Log(LoggerName, level, message, ex);
        }
    }
}

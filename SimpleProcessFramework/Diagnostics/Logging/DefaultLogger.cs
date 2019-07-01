using Spfx.Utilities;
using System;

namespace Spfx.Diagnostics.Logging
{
    internal class DefaultLogger : ILogger
    {
        private readonly ILogListener m_listener;
        private readonly string m_name;

        public ConfiguredLogger? Debug => FromLevel(LogTraceLevel.Debug);
        public ConfiguredLogger? Info => FromLevel(LogTraceLevel.Info);
        public ConfiguredLogger? Warn => FromLevel(LogTraceLevel.Warn);
        public ConfiguredLogger? Error => FromLevel(LogTraceLevel.Error);

        internal LogTraceLevel EnabledLevels { get; private set; }

        public DefaultLogger(ILogListener listener, string loggerName)
        {
            Guard.ArgumentNotNull(listener, nameof(listener));
            Guard.ArgumentNotNullOrEmpty(loggerName, nameof(loggerName));

            m_listener = listener;
            m_name = loggerName;
            EnabledLevels = LogTraceLevel.All;
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
            return (EnabledLevels & level) == level;
        }

        public void Trace(LogTraceLevel level, string message, Exception ex = null)
        {
            if (IsEnabled(level))
                m_listener.Log(m_name, level, message, ex);
        }
    }
}

using System;
using System.ComponentModel;

namespace Spfx.Utilities.Diagnostics
{
    [EditorBrowsable(EditorBrowsableState.Never)] // marking as Browsable-Never to avoid polluting people's intellisense with very common names
    public struct ConfiguredLogger
    {
        private readonly ILogger m_logger;
        private readonly LogTraceLevel m_level;

        public ConfiguredLogger(ILogger logger, LogTraceLevel level)
        {
            m_logger = logger;
            m_level = level;
        }

        public void Trace(string message)
        {
            m_logger.Trace(m_level, message);
        }

        public void Trace(Exception ex, string message)
        {
            m_logger.Trace(m_level, message, ex);
        }
    }
}
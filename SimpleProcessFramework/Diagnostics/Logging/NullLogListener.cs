using System;

namespace Spfx.Diagnostics.Logging
{
    internal class NullLogListener : ILogListener
    {
        public LogTraceLevel GetEnabledLevels(ILogger l) => 0;

        public void Log(string name, LogTraceLevel level, string message, Exception ex = null)
        {
        }
    }
}

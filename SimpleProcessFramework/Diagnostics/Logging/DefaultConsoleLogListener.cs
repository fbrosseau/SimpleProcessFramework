using System;

namespace Spfx.Diagnostics.Logging
{
    public class DefaultConsoleLogListener : ILogListener
    {
        public void Log(string name, LogTraceLevel level, string message, Exception ex = null)
        {
            var now = DateTime.Now;
            Console.WriteLine($"{now:T}|{name} [{level}]: {message}");
        }

        public LogTraceLevel GetEnabledLevels(ILogger l) => LogTraceLevel.All;
    }
}

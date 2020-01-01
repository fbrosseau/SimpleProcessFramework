using System;

namespace Spfx.Diagnostics.Logging
{
    public class ConsoleOutputLogListener : ILogListener
    {
        public void Log(string name, LogTraceLevel level, string message, Exception ex = null)
        {
            var now = DateTime.Now;
            if (ex is null)
                Console.WriteLine($"{now:T}|{name} [{level}]: {message}");
            else
                Console.WriteLine($"{now:T}|{name} [{level}]: {message}\r\n{ex}");
        }

        public LogTraceLevel GetEnabledLevels(ILogger l) => LogTraceLevel.All;
    }
}

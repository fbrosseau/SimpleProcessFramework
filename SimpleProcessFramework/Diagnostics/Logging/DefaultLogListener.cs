using System;

namespace Spfx.Diagnostics.Logging
{
    internal class DefaultLogListener : ILogListener
    {
        public void Log(string name, LogTraceLevel level, string message, Exception ex = null)
        {
            var now = DateTime.Now;
            Console.WriteLine($"{now:T}|{name} [{level}]: {message}");
        }
    }
}

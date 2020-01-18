using Spfx.Reflection;
using System;
using System.IO;

namespace Spfx.Diagnostics.Logging
{
    public class ConsoleOutputLogListener : ILogListener
    {
        private readonly TextWriter m_out;

        public ConsoleOutputLogListener(ITypeResolver typeResolver)
        {
            var console = typeResolver.CreateSingleton<IConsoleProvider>();
            m_out = console.Out;
        }

        public void Log(string name, LogTraceLevel level, string message, Exception ex = null)
        {
            var now = DateTime.Now;
            if (ex is null)
                m_out.WriteLine($"{now:T}|{name} [{level}]: {message}");
            else
                m_out.WriteLine($"{now:T}|{name} [{level}]: {message}\r\n{ex}");
        }

        public LogTraceLevel GetEnabledLevels(ILogger l) => LogTraceLevel.All;
    }
}

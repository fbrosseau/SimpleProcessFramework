using System;

namespace Spfx.Diagnostics.Logging
{
    internal class NullLogListener : ILogListener
    {
        public void Log(string name, LogTraceLevel level, string message, Exception ex = null)
        {
        }
    }
}

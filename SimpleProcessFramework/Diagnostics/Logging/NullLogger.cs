using System;

namespace Spfx.Diagnostics.Logging
{
    public class NullLogger : ILogger
    {
        public static ILogger Logger { get; } = new NullLogger();

        public ConfiguredLogger? Debug => null;
        public ConfiguredLogger? Info => null;
        public ConfiguredLogger? Warn => null;
        public ConfiguredLogger? Error => null;
        public ConfiguredLogger? FromLevel(LogTraceLevel level) => null;

        public void Dispose()
        {
        }

        public void Trace(LogTraceLevel level, string message, Exception ex = null)
        {
        }
    }
}

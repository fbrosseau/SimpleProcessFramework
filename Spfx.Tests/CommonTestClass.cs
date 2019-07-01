using Spfx.Reflection;
using Spfx.Diagnostics.Logging;

namespace Spfx.Tests
{
    public abstract class CommonTestClass
    {
        public const int DefaultTestTimeout = TestUtilities.DefaultTestTimeout;

        private static readonly ILogger s_logger = DefaultTypeResolverFactory.DefaultTypeResolver.CreateSingleton<ILoggerFactory>().GetLogger(typeof(CommonTestClass));

        protected static void Log(string msg)
        {
            s_logger.Info?.Trace(msg);
        }
    }
}

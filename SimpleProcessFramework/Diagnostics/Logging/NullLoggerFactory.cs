using System;

namespace Spfx.Diagnostics.Logging
{
    internal class NullLoggerFactory : ILoggerFactory
    {
        public static NullLoggerFactory Instance { get; } = new NullLoggerFactory();

        public ILogger GetLogger(Type loggedType, bool uniqueInstance = false) 
            => NullLogger.Logger;
    }
}

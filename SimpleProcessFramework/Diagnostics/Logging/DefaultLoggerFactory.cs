using System;

namespace Spfx.Diagnostics.Logging
{
    internal class DefaultLoggerFactory : ILoggerFactory
    {
        private readonly ILogListener m_listener;

        public DefaultLoggerFactory(ILogListener listener)
        {
            m_listener = listener;
        }

        public ILogger GetLogger(Type loggedType, bool uniqueInstance = false)
        {
            return new DefaultLogger(m_listener, loggedType.FullName);
        }
    }
}

using Spfx.Reflection;
using System;
using System.ComponentModel;

namespace Spfx.Diagnostics.Logging
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class DefaultLoggerFactory : ILoggerFactory
    {
        private readonly ILogListener m_listener;

        public DefaultLoggerFactory(ILogListener listener)
        {
            m_listener = listener;
        }

        public DefaultLoggerFactory(ITypeResolver typeResolver)
            : this(typeResolver.CreateSingleton<ILogListener>())
        {
        }

        public ILogger GetLogger(Type loggedType, bool uniqueInstance = false, string friendlyName = null)
        {
            var name = loggedType.FullName;
            if (!string.IsNullOrWhiteSpace(friendlyName))
                name += "#" + friendlyName;

            var l = new DefaultLogger(m_listener, name);
            l.EnabledLevels = m_listener.GetEnabledLevels(l);
            return l;
        }
    }
}
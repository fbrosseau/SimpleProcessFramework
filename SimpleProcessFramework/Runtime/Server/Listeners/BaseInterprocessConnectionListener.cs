using Spfx.Diagnostics.Logging;
using Spfx.Reflection;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server.Listeners
{
    public abstract class BaseInterprocessConnectionListener : AsyncDestroyable, IConnectionListener
    {
        public ITypeResolver TypeResolver { get; private set; }
        protected ILogger Logger { get; private set; }

        public virtual string FriendlyName => GetType().Name;

        private IClientConnectionManager m_connectionManager;

        public virtual void Start(ITypeResolver typeResolver)
        {
            TypeResolver = typeResolver;
            Logger = typeResolver.GetLogger(GetType(), uniqueInstance: true, FriendlyName);
            m_connectionManager = typeResolver.GetSingleton<IClientConnectionManager>();
        }

        protected void RaiseConnectionReceived(IInterprocessClientChannel client)
        {
            m_connectionManager.RegisterClientChannel(client);
        }
    }
}
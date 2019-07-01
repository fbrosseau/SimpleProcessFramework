using Spfx.Reflection;

namespace Spfx.Runtime.Server.Listeners
{
    public abstract class BaseInterprocessConnectionListener : IConnectionListener
    {
        public ITypeResolver TypeResolver { get; private set; }

        private IClientConnectionManager m_connectionManager;

        public virtual void Dispose()
        {
        }

        public virtual void Start(ITypeResolver typeResolver)
        {
            TypeResolver = typeResolver;
            m_connectionManager = typeResolver.GetSingleton<IClientConnectionManager>();
        }

        protected void RaiseConnectionReceived(IInterprocessClientChannel client)
        {
            m_connectionManager.RegisterClientChannel(client);
        }
    }
}

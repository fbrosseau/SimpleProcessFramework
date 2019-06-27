namespace Spfx.Runtime.Server.Listeners
{
    public abstract class BaseInterprocessConnectionListener : IConnectionListener
    {
        private IClientConnectionManager m_connectionManager;

        public virtual void Dispose()
        {
        }

        public virtual void Start(IClientConnectionManager owner)
        {
            m_connectionManager = owner;
        }

        protected void RaiseConnectionReceived(IInterprocessClientChannel client)
        {
            m_connectionManager.RegisterClientChannel(client);
        }
    }
}

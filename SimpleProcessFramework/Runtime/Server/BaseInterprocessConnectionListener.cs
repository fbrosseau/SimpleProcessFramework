using System;

namespace SimpleProcessFramework.Runtime.Server
{
    public abstract class BaseInterprocessConnectionListener : IConnectionListener
    {
        public event EventHandler<IpcConnectionReceivedEventArgs> ConnectionReceived;

        public virtual void Dispose()
        {
        }

        public abstract void Start(IClientConnectionManager owner);

        protected void RaiseConnectionReceived(IInterprocessClientChannel client)
        {
            ConnectionReceived?.Invoke(this, new IpcConnectionReceivedEventArgs(client));
        }
    }
}

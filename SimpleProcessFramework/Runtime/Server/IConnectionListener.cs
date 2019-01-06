using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleProcessFramework.Runtime.Server
{
    public class IpcConnectionReceivedEventArgs : EventArgs
    {
        public IInterprocessClientChannel Client { get; }

        public IpcConnectionReceivedEventArgs(IInterprocessClientChannel client)
        {
            Client = client;
        }
    }

    public interface IConnectionListener : IDisposable
    {
        event EventHandler<IpcConnectionReceivedEventArgs> ConnectionReceived;

        void Start();
    }
}

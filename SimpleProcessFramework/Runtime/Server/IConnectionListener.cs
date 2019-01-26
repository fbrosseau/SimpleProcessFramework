using System;

namespace SimpleProcessFramework.Runtime.Server
{
    public interface IConnectionListener : IDisposable
    {
        void Start(IClientConnectionManager clientConnectionsManager);
    }
}

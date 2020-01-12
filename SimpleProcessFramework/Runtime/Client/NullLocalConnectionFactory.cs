using System;

namespace Spfx.Runtime.Client
{
    internal class NullLocalConnectionFactory : ILocalConnectionFactory
    {
        public bool IsLoopback(ref ProcessEndpointAddress addr) => false;
        public IClientInterprocessConnection GetLoopbackConnection() => throw new NotSupportedException("This instance cannot provide a local connection");
    }
}
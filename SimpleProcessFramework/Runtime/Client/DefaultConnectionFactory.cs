using SimpleProcessFramework.Serialization;
using System;
using System.Collections.Generic;

namespace SimpleProcessFramework.Runtime.Client
{
    public interface IClientConnectionFactory
    {
        IClientInterprocessConnection GetConnection(ProcessEndpointAddress destination);
    }

    internal class DefaultClientConnectionFactory : IClientConnectionFactory
    {
        private readonly Dictionary<string, IClientInterprocessConnection> m_connections = new Dictionary<string, IClientInterprocessConnection>(StringComparer.OrdinalIgnoreCase);
        private readonly IBinarySerializer m_serializer;

        public DefaultClientConnectionFactory(IBinarySerializer serializer)
        {
            m_serializer = serializer;
        }

        public IClientInterprocessConnection GetConnection(ProcessEndpointAddress destination)
        {
            var hostAuthority = destination.HostAuthority;

            IClientInterprocessConnection conn;

            lock (m_connections)
            {
                if (m_connections.TryGetValue(hostAuthority, out conn))
                    return conn;
            }

            var newConn = CreateNewConnection(destination);

            lock (m_connections)
            {
                if (m_connections.TryGetValue(hostAuthority, out conn))
                {
                    newConn?.Dispose();
                    return conn;
                }

                m_connections[hostAuthority] = newConn;
            }

            newConn.Initialize();
            return newConn;
        }

        private IClientInterprocessConnection CreateNewConnection(ProcessEndpointAddress destination)
        {
            return new ClientRemoteInterprocessConnection(destination.HostEndpoint, m_serializer);
        }
    }
}
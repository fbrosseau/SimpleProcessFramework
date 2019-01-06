using SimpleProcessFramework.Serialization;
using System;
using System.Collections.Generic;

namespace SimpleProcessFramework.Runtime.Client
{
    internal class DefaultConnectionFactory : IConnectionFactory
    {
        private readonly Dictionary<string, IInterprocessConnection> m_connections = new Dictionary<string, IInterprocessConnection>(StringComparer.OrdinalIgnoreCase);
        private readonly IBinarySerializer m_serializer;

        public DefaultConnectionFactory(IBinarySerializer serializer = null)
        {
            m_serializer = serializer ?? new DefaultBinarySerializer();
        }

        public IInterprocessConnection GetConnection(ProcessEndpointAddress destination)
        {
            var hostAuthority = destination.HostAuthority;

            IInterprocessConnection conn;

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

        private IInterprocessConnection CreateNewConnection(ProcessEndpointAddress destination)
        {
            return new ClientRemoteInterprocessConnection(destination.HostEndpoint, m_serializer);
        }
    }
}
using Spfx.Reflection;
using Spfx.Serialization;
using System;
using System.Collections.Generic;

namespace Spfx.Runtime.Client
{
    public interface IClientConnectionFactory
    {
        IClientInterprocessConnection GetConnection(ProcessEndpointAddress destination);
    }

    public abstract class DefaultClientConnectionFactory : IClientConnectionFactory
    {
        private readonly Dictionary<string, IClientInterprocessConnection> m_connections = new Dictionary<string, IClientInterprocessConnection>(StringComparer.OrdinalIgnoreCase);

        protected ITypeResolver TypeResolver { get; }

        private readonly ILocalConnectionFactory m_loopbackConnectionProvider;

        public DefaultClientConnectionFactory(ITypeResolver typeResolver)
        {
            TypeResolver = typeResolver;
            m_loopbackConnectionProvider = typeResolver.CreateSingleton<ILocalConnectionFactory>();
        }

        public IClientInterprocessConnection GetConnection(ProcessEndpointAddress destination)
        {
            bool isLoopback = m_loopbackConnectionProvider.IsLoopback(ref destination);

            var hostAuthority = destination.HostAuthority;

            IClientInterprocessConnection conn;

            lock (m_connections)
            {
                if (m_connections.TryGetValue(hostAuthority, out conn))
                    return conn;
            }

            var newConn = isLoopback ? m_loopbackConnectionProvider.GetLoopbackConnection() : CreateNewConnection(destination);
            var winner = TryRegisterConnection(destination, newConn);

            if (ReferenceEquals(winner, newConn))
                newConn.Initialize();

            return winner;
        }

        protected IClientInterprocessConnection TryRegisterConnection(ProcessEndpointAddress destination, IClientInterprocessConnection newConn)
        {
            lock (m_connections)
            {
                if (m_connections.TryGetValue(destination.HostAuthority, out var conn))
                {
                    newConn?.Dispose();
                    return conn;
                }

                m_connections[destination.HostAuthority] = newConn;
            }

            return newConn;
        }

        protected abstract IClientInterprocessConnection CreateNewConnection(ProcessEndpointAddress destination);
    }
}
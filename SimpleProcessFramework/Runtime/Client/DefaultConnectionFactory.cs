using Spfx.Reflection;
using Spfx.Runtime.Server;
using Spfx.Serialization;
using System;
using System.Collections.Generic;

namespace Spfx.Runtime.Client
{
    public interface IClientConnectionFactory
    {
        IClientInterprocessConnection GetConnection(ProcessEndpointAddress destination);
        ProcessEndpointAddress NormalizeAddress(ProcessEndpointAddress address);
    }

    internal class InternalClientConnectionFactory : DefaultClientConnectionFactory
    {
        private readonly ITypeResolver m_typeResolver;
        private readonly string m_localAuthority;
        private readonly IProcessInternal m_parent;

        public InternalClientConnectionFactory(ITypeResolver typeResolver)
            : base(typeResolver.GetSingleton<IBinarySerializer>())
        {
            m_typeResolver = typeResolver;
            m_localAuthority = typeResolver.GetSingleton<IProcess>().HostAuthority;
        }

        protected override IClientInterprocessConnection CreateNewConnection(ProcessEndpointAddress destination)
        {
            if(IsLoopback(destination))
            {
                return new ClientSameClusterInterprocessConnection(m_typeResolver);
            }

            return base.CreateNewConnection(destination);
        }

        public override ProcessEndpointAddress NormalizeAddress(ProcessEndpointAddress address)
        {
            if (!string.IsNullOrWhiteSpace(address.HostAuthority))
                return address;
            return new ProcessEndpointAddress(m_localAuthority, address.TargetProcess, address.LeafEndpoint);
        }

        private bool IsLoopback(ProcessEndpointAddress destination)
        {
            return m_localAuthority.Equals(destination.HostAuthority, StringComparison.OrdinalIgnoreCase);
        }
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
            var hostAuthority = NormalizeAddress(destination).HostAuthority;

            IClientInterprocessConnection conn;

            lock (m_connections)
            {
                if (m_connections.TryGetValue(hostAuthority, out conn))
                    return conn;
            }

            var newConn = CreateNewConnection(destination);
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

        protected virtual IClientInterprocessConnection CreateNewConnection(ProcessEndpointAddress destination)
        {
            return new ClientRemoteInterprocessConnection(destination.HostEndpoint, m_serializer);
        }

        public virtual ProcessEndpointAddress NormalizeAddress(ProcessEndpointAddress address)
        {
            return address;
        }
    }
}
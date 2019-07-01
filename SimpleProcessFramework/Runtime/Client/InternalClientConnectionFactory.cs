using Spfx.Reflection;
using Spfx.Runtime.Server;
using System;

namespace Spfx.Runtime.Client
{
    internal class InternalClientConnectionFactory : ILocalConnectionFactory
    {
        private readonly ITypeResolver m_typeResolver;
        private readonly string m_localAuthority;

        public InternalClientConnectionFactory(ITypeResolver typeResolver)
        {
            m_typeResolver = typeResolver;
            m_localAuthority = typeResolver.GetSingleton<IProcess>().HostAuthority;
        }

        public IClientInterprocessConnection GetLoopbackConnection()
        {
            return new ClientSameClusterInterprocessConnection(m_typeResolver);
        }

        private ProcessEndpointAddress NormalizeAddress(ProcessEndpointAddress address)
        {
            if (!string.IsNullOrWhiteSpace(address.HostAuthority))
                return address;
            return new ProcessEndpointAddress(m_localAuthority, address.TargetProcess, address.LeafEndpoint);
        }

        public bool IsLoopback(ref ProcessEndpointAddress destination)
        {
            destination = NormalizeAddress(destination);
            return m_localAuthority.Equals(destination.HostAuthority, StringComparison.OrdinalIgnoreCase);
        }
    }
}
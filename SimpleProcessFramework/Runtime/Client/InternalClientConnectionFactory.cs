using Spfx.Reflection;
using Spfx.Runtime.Server;
using Spfx.Serialization;
using System;

namespace Spfx.Runtime.Client
{
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
            if (IsLoopback(destination))
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
}
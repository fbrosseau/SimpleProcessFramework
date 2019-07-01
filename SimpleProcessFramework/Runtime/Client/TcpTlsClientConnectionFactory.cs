using Spfx.Reflection;

namespace Spfx.Runtime.Client
{
    public class TcpTlsClientConnectionFactory : DefaultClientConnectionFactory
    {
        public TcpTlsClientConnectionFactory(ITypeResolver typeResolver)
            : base(typeResolver)
        {
        }

        protected override IClientInterprocessConnection CreateNewConnection(ProcessEndpointAddress destination)
        {
            return new TcpTlsClientInterprocessConnection(destination.HostEndpoint, TypeResolver);
        }
    }
}
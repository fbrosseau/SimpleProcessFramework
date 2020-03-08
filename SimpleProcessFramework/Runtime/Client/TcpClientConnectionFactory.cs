using Spfx.Reflection;

namespace Spfx.Runtime.Client
{
    public class TcpClientConnectionFactory : DefaultClientConnectionFactory
    {
        public TcpClientConnectionFactory(ITypeResolver typeResolver)
            : base(typeResolver)
        {
        }

        protected override IClientInterprocessConnection CreateNewConnection(ProcessEndpointAddress destination)
        {
            return new TcpClientInterprocessConnection(destination, TypeResolver);
        }
    }
}
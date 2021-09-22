using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client
{
    internal class TcpTlsClientInterprocessConnection : TcpClientInterprocessConnection
    {
        public TcpTlsClientInterprocessConnection(ProcessEndpointAddress destination, ITypeResolver typeResolver)
            : base(destination, typeResolver)
        {
        }
        
        protected override async Task<Stream> CreateFinalStream(Stream ns)
        {
            try
            {
                var tlsAuthenticator = TypeResolver.CreateSingleton<IClientSslStreamFactory>();
                return await tlsAuthenticator.CreateStreamAndAuthenticateAsync(ns, "<Todo>").ConfigureAwait(false);
            }
            catch (AuthenticationException ex)
            {
                throw new ProxyTlsConnectionFailedException(ex);
            }
        }
    }
}
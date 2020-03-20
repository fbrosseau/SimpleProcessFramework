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
                var tlsStream = new SslStream(ns, false, delegate { return true; });
                await tlsStream.AuthenticateAsClientAsync("unused", null, SslProtocols.None, false).ConfigureAwait(false);
                return tlsStream;
            }
            catch (AuthenticationException ex)
            {
                throw new ProxyTlsConnectionFailedException(ex);
            }
        }
    }
}
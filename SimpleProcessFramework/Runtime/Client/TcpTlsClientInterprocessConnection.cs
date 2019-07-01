using Spfx.Reflection;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client
{
    internal class TcpTlsClientInterprocessConnection : TcpClientInterprocessConnection
    {
        public TcpTlsClientInterprocessConnection(EndPoint destination, ITypeResolver typeResolver)
            : base(destination, typeResolver)
        {
        }
        
        protected override async Task<Stream> CreateFinalStream(Stream ns)
        {
            var tlsStream = new SslStream(ns, false, delegate { return true; });
            await tlsStream.AuthenticateAsClientAsync("unused", null, SslProtocols.None, false);
            return tlsStream;
        }
    }
}
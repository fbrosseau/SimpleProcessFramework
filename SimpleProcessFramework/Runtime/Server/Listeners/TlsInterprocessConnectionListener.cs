using Spfx.Utilities;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Listeners
{
    public class TlsInterprocessConnectionListener : TcpInterprocessConnectionListener
    {
        private X509Certificate2 m_certificate;

        public TlsInterprocessConnectionListener(X509Certificate2 cert, int port)
          : this(cert, TcpListener.Create(port))
        {
        }

        public TlsInterprocessConnectionListener(X509Certificate2 cert, IPEndPoint ep)
            : this(cert, new TcpListener(ep))
        {
        }

        public TlsInterprocessConnectionListener(X509Certificate2 cert, TcpListener tcpListener)
            : base(tcpListener)
        {
            UpdateCertificate(cert);
        }

        public void UpdateCertificate(X509Certificate2 cert)
        {
            Guard.ArgumentNotNull(cert, nameof(cert));
            m_certificate = cert;
        }

        protected override async ValueTask<Stream> CreateFinalStream(NetworkStream ns)
        {
            var ssl = new SslStream(ns);

            try
            {
                await ssl.AuthenticateAsServerAsync(m_certificate, false, SslProtocols.None, false);
            }
            catch
            {
                ssl.Dispose();
                throw;
            }

            return ssl;
        }
    }
}

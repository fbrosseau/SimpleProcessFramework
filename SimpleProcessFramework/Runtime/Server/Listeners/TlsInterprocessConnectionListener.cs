using Spfx.Utilities;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Listeners
{
    public class TlsInterprocessConnectionListener : TcpInterprocessConnectionListener
    {
        private X509Certificate2 m_certificate;
        private IServerSslStreamFactory m_sslStreamFactory;

        public override string FriendlyName => "TCP+TLS @ " + ListenEndpoint;

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

        protected override ValueTask<Stream> CreateFinalStream(Stream ns, CancellationToken ct)
        {
            if (m_sslStreamFactory is null)
                m_sslStreamFactory = TypeResolver.CreateSingleton<IServerSslStreamFactory>();

            return new ValueTask<Stream>(m_sslStreamFactory.CreateSslStreamAndAuthenticate(ns, m_certificate, ct));
        }
    }
}

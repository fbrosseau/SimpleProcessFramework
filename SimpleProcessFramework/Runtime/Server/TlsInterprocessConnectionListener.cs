using SimpleProcessFramework.Utilities;
using SimpleProcessFramework.Serialization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Server
{
    public class TlsInterprocessConnectionListener : TcpInterprocessConnectionListener
    {
        private X509Certificate2 m_certificate;

        public TlsInterprocessConnectionListener(X509Certificate2 cert, int port, IBinarySerializer serializer = null)
          : this(cert, TcpListener.Create(port), serializer)
        {
        }

        public TlsInterprocessConnectionListener(X509Certificate2 cert, IPEndPoint ep, IBinarySerializer serializer = null)
            : this(cert, new TcpListener(ep), serializer)
        {
        }

        public TlsInterprocessConnectionListener(X509Certificate2 cert, TcpListener tcpListener, IBinarySerializer serializer)
            : base(tcpListener, serializer)
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

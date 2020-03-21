using Spfx.Utilities.Threading;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Listeners
{
    public interface IServerSslStreamFactory
    {
        Task<Stream> CreateSslStreamAndAuthenticate(Stream innerStream, X509Certificate2 defaultCertificate, System.Threading.CancellationToken ct);
    }

    public class DefaultServerSslStreamFactory : IServerSslStreamFactory
    {
        public virtual async Task<Stream> CreateSslStreamAndAuthenticate(Stream innerStream, X509Certificate2 defaultCertificate, CancellationToken ct)
        {
            var secureStream = new SslStream(innerStream);

            try
            {
                await secureStream.AuthenticateAsServerAsync(defaultCertificate, false, SslProtocols.None, false)
                    .WithCancellation(ct)
                    .ConfigureAwait(false);
                return secureStream;
            }
            catch
            {
                await secureStream.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}

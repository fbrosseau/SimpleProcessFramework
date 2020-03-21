using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client
{
    public interface IClientSslStreamFactory
    {
        Task<Stream> CreateStreamAndAuthenticateAsync(Stream rawNetworkStream, string remoteName);
    }

    public class DangerousTrustEverythingClientSslStreamFactory : IClientSslStreamFactory
    {
        public async Task<Stream> CreateStreamAndAuthenticateAsync(Stream rawNetworkStream, string remoteName)
        {
            var secureStream = new SslStream(rawNetworkStream, false, delegate { return true; });
            try
            {
                await secureStream.AuthenticateAsClientAsync(remoteName, null, SslProtocols.None, false).ConfigureAwait(false);
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
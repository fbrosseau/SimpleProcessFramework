using Spfx.Io;
using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities.Threading;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Listeners
{
    public abstract class StreamInterprocessConnectionListener : BaseInterprocessConnectionListener
    {
        private IBinarySerializer m_serializer;
        private TimeSpan m_receiveConnectionTimeout;
        
        internal const int MagicStartCode = unchecked((int)0xF00DBEEF);
        internal static byte[] MagicStartCodeBytes { get; } = BitConverter.GetBytes(MagicStartCode);

        public override void Start(ITypeResolver typeResolver)
        {
            base.Start(typeResolver);
            m_serializer = typeResolver.CreateSingleton<IBinarySerializer>();
            m_receiveConnectionTimeout = typeResolver.CreateSingleton<ProcessClusterConfiguration>().ReceiveConnectionTimeout;
        }

        protected async Task CreateChannelFromStream(Stream s, string localEndpoint, string remoteEndpoint)
        {
            try
            {
                Logger.Debug?.Trace($"Starting CreateChannelFromStream for {remoteEndpoint}->{localEndpoint}");
                var clientHandshakeTask = DoHandshake(s);
                if (!await clientHandshakeTask.TryWaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false))
                    return;

                var finalStream = await clientHandshakeTask.ConfigureAwait(false);

                var conn = new ServerInterprocessChannel(TypeResolver, finalStream, finalStream, localEndpoint, remoteEndpoint);
                Logger.Debug?.Trace($"Successfully received connection {remoteEndpoint}: {conn.UniqueId}");
                RaiseConnectionReceived(conn);
            }
            catch (Exception ex)
            {
                Logger.Warn?.Trace(ex, $"Failed to accept a connection from {remoteEndpoint}");

                s.Dispose();
                throw;
            }
        }

        private async Task<Stream> DoHandshake(Stream rawStream)
        {
            Stream clientStream = null;

            try
            {
                using var cts = new CancellationTokenSource(m_receiveConnectionTimeout);
                var ct = cts.Token;

                using var ctr = ct.Register(() => { rawStream.DisposeAsync().FireAndForget(); });

                clientStream = await CreateFinalStream(rawStream, ct).ConfigureAwait(false);

                var code = await clientStream.ReadLittleEndian32BitInt(ct).ConfigureAwait(false);
                if (code != MagicStartCode)
                {
                    string details = null;
                    if ((code & 0xFFFF) == 0x0316)
                    {
                        details = "The data received appears to be TLS handshake data. This may indicate that the client is configured to connect over TLS, while the server is configured for unencrypted traffic.";
                    }
                    else
                    {
                        details = "Unknown data was received";
                    }

                    throw new InvalidClientConnectionProtocolException(details);
                }

                using var msg = await clientStream.ReadLengthPrefixedBlockAsync(RemoteClientConnectionRequest.MaximumMessageSize, ct: ct).ConfigureAwait(false);
                /*var clientMessage = */
                m_serializer.Deserialize<object>(msg);

                using var serializedResponse = m_serializer.Serialize<object>(new RemoteClientConnectionResponse
                {
                    Success = true
                }, lengthPrefix: true);

                await serializedResponse.CopyToAsync(clientStream, ct).ConfigureAwait(false);
                await clientStream.FlushAsync(ct).ConfigureAwait(false);
                return clientStream;
            }
            catch 
            {
                if (clientStream != null)
                    await clientStream.DisposeAsync().ConfigureAwait(false);
                await rawStream.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        protected virtual ValueTask<Stream> CreateFinalStream(Stream ns, CancellationToken ct)
        {
            return new ValueTask<Stream>(ns);
        }
    }
}

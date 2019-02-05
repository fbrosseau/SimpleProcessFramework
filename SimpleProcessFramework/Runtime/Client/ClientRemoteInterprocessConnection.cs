using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities;
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client
{
    internal class ClientRemoteInterprocessConnection : AbstractClientInterprocessConnection
    {
        private readonly EndPoint m_destination;

        public ClientRemoteInterprocessConnection(EndPoint destination, IBinarySerializer serializer)
            : base(serializer)
        {
            m_destination = destination;
        }

        protected override async Task<ProcessEndpointDescriptor> GetRemoteEndpointMetadata(ProcessEndpointAddress destination, ReflectedTypeInfo type)
        {
            return (ProcessEndpointDescriptor)await SerializeAndSendMessage(new EndpointDescriptionRequest
            {
                Destination = destination
            });
        }

        internal override async Task<(Stream readStream, Stream writeStream)> ConnectStreamsAsync()
        {
            using (var disposeBag = new DisposeBag())
            {
                var client = new Socket(SocketType.Stream, ProtocolType.Tcp);
                disposeBag.Add(client);

                await Task.Factory.FromAsync((cb, s) => client.BeginConnect(m_destination, cb, s), client.EndConnect, null);

                var ns = disposeBag.Add(new NetworkStream(client));
                var tlsStream = disposeBag.Add(new SslStream(ns, false, delegate { return true; }));

                var timeout = Task.Delay(TimeSpan.FromSeconds(30));
                var auth = Authenticate(tlsStream);

                var winner = await Task.WhenAny(timeout, auth);

                if (ReferenceEquals(winner, timeout))
                    throw new TimeoutException("Authentication timed out");

                disposeBag.ReleaseAll();
                return (tlsStream, tlsStream);
            }
        }

        private async Task Authenticate(SslStream tlsStream)
        {
            await tlsStream.AuthenticateAsClientAsync("unused", null, SslProtocols.None, false);

            var hello = BinarySerializer.Serialize<object>(new RemoteClientConnectionRequest(), lengthPrefix: true);
            await hello.CopyToAsync(tlsStream);

            var responseStream = await tlsStream.ReadLengthPrefixedBlock();
            var response = (RemoteClientConnectionResponse)BinarySerializer.Deserialize<object>(responseStream);

            if (response.Success)
                return;

            throw new RemoteConnectionException(string.IsNullOrWhiteSpace(response.Error) ? "The connection was refused by the remote host" : response.Error);
        }
    }
}
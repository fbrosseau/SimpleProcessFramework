using Spfx.Io;
using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Messages;
using Spfx.Utilities;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client
{
    internal class TcpClientInterprocessConnection : StreamBasedClientInterprocessConnection
    {
        protected EndPoint Destination { get; }

        public TcpClientInterprocessConnection(EndPoint destination, ITypeResolver typeResolver)
            : base(typeResolver)
        {
            Destination = destination;
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
                var client = SocketUtilities.CreateSocket(SocketType.Stream, ProtocolType.Tcp);
                disposeBag.Add(client);

                await Task.Factory.FromAsync((cb, s) => client.BeginConnect(Destination, cb, s), client.EndConnect, null);

                var ns = disposeBag.Add(new NetworkStream(client));

                var finalStream = disposeBag.Add(await CreateFinalStream(ns));

                var timeout = Task.Delay(TimeSpan.FromSeconds(30));
                var auth = Authenticate(finalStream);

                var winner = await Task.WhenAny(timeout, auth);

                if (ReferenceEquals(winner, timeout))
                    throw new TimeoutException("Authentication timed out");

                disposeBag.ReleaseAll();
                return (finalStream, finalStream);
            }
        }

        protected virtual Task<Stream> CreateFinalStream(Stream ns)
        {
            return Task.FromResult(ns);
        }

        private async Task Authenticate(Stream tlsStream)
        {
            using (var hello = BinarySerializer.Serialize<object>(new RemoteClientConnectionRequest(), lengthPrefix: true))
            {
                await hello.CopyToAsync(tlsStream).ConfigureAwait(false);
            }

            using var responseStream = await tlsStream.ReadLengthPrefixedBlockAsync();
            var response = (RemoteClientConnectionResponse)BinarySerializer.Deserialize<object>(responseStream);

            if (response.Success)
                return;

            throw new RemoteConnectionException(string.IsNullOrWhiteSpace(response.Error) ? "The connection was refused by the remote host" : response.Error);
        }
    }
}
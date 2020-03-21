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
        public TcpClientInterprocessConnection(ProcessEndpointAddress destination, ITypeResolver typeResolver)
            : base(destination, typeResolver)
        {
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
            using var disposeBag = new DisposeBag();

            var client = SocketUtilities.CreateSocket(SocketType.Stream, ProtocolType.Tcp);
            disposeBag.Add(client);

            try
            {
                await Task.Factory.FromAsync((cb, s) => client.BeginConnect(Destination.HostEndpoint, cb, s), client.EndConnect, null);
            }
            catch (SocketException ex)
            {
                throw new ProxySocketConnectionFailedException(ex);
            }
            
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

        protected virtual Task<Stream> CreateFinalStream(Stream ns)
        {
            return Task.FromResult(ns);
        }
    }
}
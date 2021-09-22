using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Messages;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.IO;
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
            }).ConfigureAwait(false);
        }

        internal override async Task<(Stream readStream, Stream writeStream)> ConnectStreamsAsync()
        {
            using var disposeBag = new DisposeBag();

            var client = SocketUtilities.CreateSocket(SocketType.Stream, ProtocolType.Tcp);
            disposeBag.Add(client);

            try
            {
                await Task.Factory.FromAsync((cb, s) => client.BeginConnect(Destination.HostEndpoint, cb, s), client.EndConnect, null).ConfigureAwait(false);
            }
            catch (SocketException ex)
            {
                throw new ProxySocketConnectionFailedException(ex);
            }

            var ns = disposeBag.Add(new NetworkStream(client));

            var finalStream = disposeBag.Add(await CreateFinalStream(ns).ConfigureAwait(false));

            var auth = Authenticate(finalStream);

            if (!await auth.TryWaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false))
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
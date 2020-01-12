using Spfx.Io;
using Spfx.Reflection;
using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Listeners
{
    public class TcpInterprocessConnectionListener : BaseInterprocessConnectionListener, ITcpListener
    {
        private readonly TcpListener m_listener;
        private Task m_listenLoop;
        private IBinarySerializer m_serializer;

        public IPEndPoint ListenEndpoint => (IPEndPoint)m_listener.LocalEndpoint;
        EndPoint IExternalConnectionsListener.ListenEndpoint => ListenEndpoint;

        public TcpInterprocessConnectionListener(int port)
            : this(TcpListener.Create(port))
        {
        }

        public TcpInterprocessConnectionListener(IPEndPoint ep)
            : this(new TcpListener(ep))
        {
        }

        protected TcpInterprocessConnectionListener(TcpListener tcpListener)
        {
            m_listener = tcpListener;
        }

        public override void Dispose()
        {
            m_listener.Stop();
            m_listenLoop.FireAndForget();
        }

        public override void Start(ITypeResolver typeResolver)
        {
            base.Start(typeResolver);
            m_serializer = typeResolver.CreateSingleton<IBinarySerializer>();
            m_listener.Start();
            m_listenLoop = ListenLoop();
        }

        private async Task ListenLoop()
        {
            while (true)
            {
                var s = await m_listener.AcceptSocketAsync();
                HandleSocket(s).FireAndForget();
            }
        }

        private async Task HandleSocket(Socket s)
        {
            using var disposeBag = new DisposeBag();

            var ns = disposeBag.Add(new NetworkStream(s, ownsSocket: true));

            var clientHandshakeTask = DoHandshake(ns);
            if (!await clientHandshakeTask.WaitAsync(TimeSpan.FromSeconds(30)))
                return;

            var finalStream = await clientHandshakeTask;

            var conn = new ServerInterprocessChannel(TypeResolver, finalStream, s.LocalEndPoint.ToString(), s.RemoteEndPoint.ToString());
            RaiseConnectionReceived(conn);
            disposeBag.ReleaseAll();
        }

        private async Task<Stream> DoHandshake(NetworkStream rawStream)
        {
            var clientStream = await CreateFinalStream(rawStream);

            using var msg = await clientStream.ReadLengthPrefixedBlockAsync(RemoteClientConnectionRequest.MaximumMessageSize);
            /*var clientMessage = */
            m_serializer.Deserialize<object>(msg);

            using var serializedResponse = m_serializer.Serialize<object>(new RemoteClientConnectionResponse
            {
                Success = true
            }, lengthPrefix: true);

            await serializedResponse.CopyToAsync(clientStream).ConfigureAwait(false);
            await clientStream.FlushAsync();
            return clientStream;
        }

        protected virtual ValueTask<Stream> CreateFinalStream(NetworkStream ns)
        {
            return new ValueTask<Stream>(ns);
        }
    }
}

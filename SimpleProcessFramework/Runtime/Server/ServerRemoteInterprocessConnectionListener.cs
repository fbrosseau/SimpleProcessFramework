using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server
{
    public class TcpInterprocessConnectionListener : BaseInterprocessConnectionListener
    {
        private readonly TcpListener m_listener;
        private readonly IBinarySerializer m_serializer;
        private Task m_listenLoop;

        public TcpInterprocessConnectionListener(int port, IBinarySerializer serializer = null)
            : this(TcpListener.Create(port), serializer)
        {
        }

        public TcpInterprocessConnectionListener(IPEndPoint ep, IBinarySerializer serializer = null)
            : this(new TcpListener(ep), serializer)
        {
        }

        protected TcpInterprocessConnectionListener(TcpListener tcpListener, IBinarySerializer serializer)
        {
            m_listener = tcpListener;
            m_serializer = serializer ?? new DefaultBinarySerializer();
        }

        public override void Dispose()
        {
            m_listener.Stop();
        }

        public override void Start(IClientConnectionManager owner)
        {
            m_listener.Start();
            m_listenLoop = ListenLoop();
        }

        private async Task ListenLoop()
        {
            while(true)
            {
                var s = await m_listener.AcceptSocketAsync();
                HandleSocket(s).FireAndForget();
            }
        }

        private async Task HandleSocket(Socket s)
        {
            using (var disposeBag = new DisposeBag())
            {
                var ns = disposeBag.Add(new NetworkStream(s));

                var clientHandshakeTask = DoHandshake(ns);
                if (!await clientHandshakeTask.WaitAsync(TimeSpan.FromSeconds(30)))
                    return;

                var finalStream = await clientHandshakeTask;

                var conn = new ServerInterprocessChannel(m_serializer, finalStream, s.LocalEndPoint.ToString(), s.RemoteEndPoint.ToString());
                disposeBag.ReleaseAll();
                RaiseConnectionReceived(conn);
            }
        }

        private async Task<Stream> DoHandshake(NetworkStream rawStream)
        {
            var clientStream = await CreateFinalStream(rawStream);

            var msg = await clientStream.ReadLengthPrefixedBlock(RemoteClientConnectionRequest.MaximumMessageSize);
            var clientMessage = m_serializer.Deserialize<object>(msg);

            var serializedResponse = m_serializer.Serialize<object>(new RemoteClientConnectionResponse
            {
                Success = true
            }, lengthPrefix: true);

            using (serializedResponse)
            {
                serializedResponse.CopyTo(clientStream);
            }

            await clientStream.FlushAsync();
            return clientStream;
        }

        protected virtual ValueTask<Stream> CreateFinalStream(NetworkStream ns)
        {
            return new ValueTask<Stream>(ns);
        }
    }
}

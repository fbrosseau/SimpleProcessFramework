using Spfx.Reflection;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Listeners
{
    public class TcpInterprocessConnectionListener : StreamInterprocessConnectionListener, ITcpListener
    {
        private readonly TcpListener m_listener;
        private Task m_listenLoop;

        public IPEndPoint ListenEndpoint { get; private set; }
        public IPEndPoint ConnectEndpoint { get; private set; }
        EndPoint IExternalConnectionsListener.ListenEndpoint => ListenEndpoint;
        EndPoint IExternalConnectionsListener.ConnectEndpoint => ConnectEndpoint;

        public override string FriendlyName => "TCP @ " + ListenEndpoint;

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

        protected override void OnDispose()
        {
            m_listener.Stop();
            m_listenLoop?.FireAndForget();
            base.OnDispose();
        }

        public override void Start(ITypeResolver typeResolver)
        {
            m_listener.Start();
            ListenEndpoint = (IPEndPoint)m_listener.LocalEndpoint;

            if (IPAddress.Any.Equals(ListenEndpoint.Address))
                ConnectEndpoint = new IPEndPoint(IPAddress.Loopback, ListenEndpoint.Port);
            else if (IPAddress.IPv6Any.Equals(ListenEndpoint.Address))
                ConnectEndpoint = new IPEndPoint(IPAddress.IPv6Loopback, ListenEndpoint.Port);
            else
                ConnectEndpoint = ListenEndpoint;

            base.Start(typeResolver);

            m_listenLoop = ListenLoop();
        }

        private async Task ListenLoop()
        {
            while (true)
            {
                var s = await m_listener.AcceptSocketAsync();
                Task.Run(() =>
                    CreateChannelFromStream(
                        new NetworkStream(s, ownsSocket: true),
                        EndpointHelper.EndpointToString(s.LocalEndPoint),
                        EndpointHelper.EndpointToString(s.RemoteEndPoint))).FireAndForget();
            }
        }
    }
}

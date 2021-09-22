using Spfx.Utilities;
using Spfx.Utilities.Runtime;
using Spfx.Utilities.Threading;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes.Windows
{
    internal sealed class WslProcessSpawnPunchHandles : AbstractProcessInitializer
    {
        private readonly Socket m_listenSocket;
        private NetworkStream m_networkStream;
        private readonly string m_linuxAddressName;
        private static readonly Lazy<string> s_tempFolderWslPath = new Lazy<string>(GetWslTempFolder);

        public override bool UsesStdIn => true;

        private static string GetWslTempFolder()
        {
            var temp = Path.GetTempPath();
            if (!temp.EndsWith("\\"))
                temp += "\\";
            return WslUtilities.GetLinuxPath(temp);
        }

        public WslProcessSpawnPunchHandles()
        {
            m_listenSocket = SocketUtilities.CreateUnixSocket();
            var filename = Guid.NewGuid().ToString("N");
            m_linuxAddressName = s_tempFolderWslPath.Value + filename;
            var tempAddress = Path.Combine(Path.GetTempPath(), filename);
            m_listenSocket.Bind(new UnixDomainSocketEndPoint(tempAddress));
            m_listenSocket.Listen(1);
        }

        public override ValueTask InitializeAsync(ProcessSpawnPunchPayload initData, CancellationToken ct)
        {
            initData.ReadPipe = m_linuxAddressName;
            return base.InitializeAsync(initData, ct);
        }

        public override async ValueTask CompleteHandshakeAsync()
        {
            using (CancellationToken.RegisterDispose(this))
            {
                var s = SocketUtilities.CreateUnixSocket();
                try
                {
                    var accept = await m_listenSocket.AcceptAsync(s).ConfigureAwait(false);
                    m_networkStream = new NetworkStream(accept);
                    m_listenSocket.Dispose();
                }
                catch
                {
                    s.Dispose();
                    throw;
                }
            }
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            DisposeAllHandles();
        }

        public override void DisposeAllHandles()
        {
            base.DisposeAllHandles();
            m_listenSocket.Dispose();
            m_networkStream?.Dispose();
        }

        public override (Stream readStream, Stream writeStream) AcquireIOStreams()
        {
            var s = m_networkStream;
            m_networkStream = null;
            return (s, s);
        }
    }
}
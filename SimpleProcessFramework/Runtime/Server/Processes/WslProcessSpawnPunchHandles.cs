using Spfx.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes
{
    internal sealed class WslProcessSpawnPunchHandles : IRemoteProcessInitializer
    {
        private readonly Socket m_listenSocket;
        private Socket m_acceptedSocket;
        private readonly string m_linuxAddressName;
        private static readonly Lazy<string> s_tempFolderWslPath = new Lazy<string>(() => WslUtilities.GetLinuxPath(Path.GetTempPath()), false);

        public WslProcessSpawnPunchHandles()
        {
            m_listenSocket = SocketUtilities.CreateSocket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var filename = Guid.NewGuid().ToString("N");
            m_linuxAddressName = s_tempFolderWslPath.Value + filename;
            var tempAddress = Path.Combine(Path.GetTempPath(), filename);
            m_listenSocket.Bind(new UnixDomainSocketEndPoint(tempAddress));
            m_listenSocket.Listen(1);
        }

        public Stream ReadStream { get; private set; }
        public Stream WriteStream { get; private set; }

        ValueTask IRemoteProcessInitializer.InitializeAsync(CancellationToken ct)
        {
            return default;
        }

        public async ValueTask CompleteHandshakeAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            using (ct.Register(s => ((WslProcessSpawnPunchHandles)s).Dispose(), this, false))
            {
                m_acceptedSocket = await m_listenSocket.AcceptAsync();
                WriteStream = ReadStream = new NetworkStream(m_acceptedSocket);
            }
        }

        public void Dispose()
        {
            DisposeAllHandles();
        }

        public void DisposeAllHandles()
        {
            m_acceptedSocket?.Dispose();
            m_listenSocket?.Dispose();
        }

        public string FinalizeInitDataAndSerialize(Process targetProcess, ProcessSpawnPunchPayload remotePunchPayload)
        {
            remotePunchPayload.ReadPipe = m_linuxAddressName;
            return remotePunchPayload.SerializeToString();
        }

        public void HandleProcessCreatedInLock(Process targetProcess, ProcessSpawnPunchPayload initData)
        {
        }

        public void InitializeInLock()
        {
        }
    }
}
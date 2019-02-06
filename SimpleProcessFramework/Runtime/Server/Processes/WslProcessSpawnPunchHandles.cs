using Spfx.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes
{
    internal sealed class WslProcessSpawnPunchHandles : IProcessSpawnPunchHandles
    {
        private readonly Socket m_listenSocket;
        private Socket m_acceptedSocket;
        private readonly string m_linuxAddressName;
        private static readonly Lazy<string> s_tempFolderWslPath = new Lazy<string>(() => WslUtilities.GetLinuxPath(Path.GetTempPath()), false);

        public WslProcessSpawnPunchHandles()
        {
            m_listenSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
            var filename = Guid.NewGuid().ToString("N");
            m_linuxAddressName = s_tempFolderWslPath.Value + filename;
            var tempAddress = Path.Combine(Path.GetTempPath(), filename);
            m_listenSocket.Bind(SocketUtilities.CreateUnixEndpoint(tempAddress));
            m_listenSocket.Listen(5);
        }

        public Stream ReadStream { get; private set; }
        public Stream WriteStream { get; private set; }

        public async Task CompleteHandshakeAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            m_acceptedSocket = await Task.Factory.FromAsync((cb, s) => m_listenSocket.BeginAccept(cb, s), m_listenSocket.EndAccept, null);
            WriteStream = ReadStream = new NetworkStream(m_acceptedSocket);
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
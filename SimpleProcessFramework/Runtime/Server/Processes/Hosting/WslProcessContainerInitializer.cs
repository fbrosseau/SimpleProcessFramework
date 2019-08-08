using Spfx.Utilities;
using System.Threading.Tasks;
using System.Collections.Generic;
using Spfx.Reflection;
using Spfx.Io;
using System.Net.Sockets;

namespace Spfx.Runtime.Server.Processes.Hosting
{
    internal class WslProcessContainerInitializer : RemoteProcessContainerInitializer
    {
        private readonly NetworkStream m_stream;

        public WslProcessContainerInitializer(ProcessSpawnPunchPayload payload, ITypeResolver typeResolver) 
            : base(payload, typeResolver)
        {
            Logger.Info?.Trace("Creating Unix socket to " + payload.ReadPipe);
            var sock = SocketUtilities.CreateSocket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            sock.Connect(new UnixDomainSocketEndPoint(payload.ReadPipe));
            m_stream = new NetworkStream(sock);
        }

        internal override ILengthPrefixedStreamReader CreateReader()
        {
            return new AsyncLengthPrefixedStreamReader(m_stream);
        }

        internal override ILengthPrefixedStreamWriter CreateWriter()
        {
            return new AsyncLengthPrefixedStreamWriter(m_stream);
        }

        internal override IEnumerable<Task> GetShutdownEvents()
        {
            yield break;
        }

        protected override void OnDispose()
        {
            if (!InitSucceeded)
            {
                m_stream?.Dispose();
            }

            base.OnDispose();
        }
    }
}
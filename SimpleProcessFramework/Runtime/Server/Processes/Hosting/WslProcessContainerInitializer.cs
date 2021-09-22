using Spfx.Io;
using Spfx.Reflection;
using Spfx.Utilities;
using System;
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
            var connectTask = sock.ConnectAsync(new UnixDomainSocketEndPoint(payload.ReadPipe));
            if (!connectTask.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("Connect timed out on the Unix socket. This operation should be instantaneous.");
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
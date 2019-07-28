using Spfx.Interfaces;
using Spfx.Runtime.Server;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace Spfx.Utilities
{
    internal static class SocketUtilities
    {
        [Flags]
        private enum HANDLE_FLAGS : uint
        {
            None = 0,
            INHERIT = 1,
            PROTECT_FROM_CLOSE = 2
        }

        [DllImport("kernel32.dll", SetLastError = false)]
        private static extern bool SetHandleInformation(IntPtr hObject, HANDLE_FLAGS dwMask, HANDLE_FLAGS dwFlags);

        internal static Socket CreateSocket(AddressFamily af, SocketType sockType, ProtocolType protocol)
        {
            if (NetcoreHelper.IsNetcoreAtLeastVersion(3) || !HostFeaturesHelper.IsWindows)
                return new Socket(af, sockType, protocol);

            lock (ProcessCreationUtilities.ProcessCreationLock)
            {
                var sock = new Socket(af, sockType, protocol);
                SetNotInheritable(sock);
                return sock;
            }
        }

        internal static Socket CreateSocket(SocketType sockType, ProtocolType protocol)
        {
            if (NetcoreHelper.IsNetcoreAtLeastVersion(3) || !HostFeaturesHelper.IsWindows)
                return new Socket(sockType, protocol);

            lock (ProcessCreationUtilities.ProcessCreationLock)
            {
                var sock = new Socket(sockType, protocol);
                SetNotInheritable(sock);
                return sock;
            }
        }

        private static void SetNotInheritable(Socket sock)
        {
            SetHandleInformation(sock.Handle, HANDLE_FLAGS.INHERIT, 0);
        }

        public static EndPoint CreateUnixEndpoint(string addr)
        {
#if NETCOREAPP || NETSTANDARD2_1_PLUS
            return new UnixDomainSocketEndPoint(addr);
#else
            if (HostFeaturesHelper.LocalProcessKind.IsNetcore())
            {
                Type t = Type.GetType("System.Net.Sockets.UnixDomainSocketEndPoint, System.Net.Sockets");
                if (t != null)
                    return (EndPoint)Activator.CreateInstance(t, addr);
            }

            return new UnixEndpoint(addr);
#endif
        }

        public class UnixEndpoint : EndPoint
        {
            private static readonly UTF8Encoding s_rawUtf8 = new UTF8Encoding(false, false);

            public static UnixEndpoint Empty { get; } = new UnixEndpoint("");
            public string Address { get; }

            public UnixEndpoint(string addr)
            {
                Address = addr;
            }

            public override SocketAddress Serialize()
            {
                var addr = new SocketAddress(AddressFamily.Unix, 300);

                var bytes = s_rawUtf8.GetBytes(Address);
                for (int i = 0; i < bytes.Length; ++i)
                {
                    addr[2 + i] = bytes[i];
                }

                return addr;
            }

            public override EndPoint Create(SocketAddress socketAddress)
            {
                int firstNull = 2;
                while (firstNull < socketAddress.Size && socketAddress[firstNull] != 0)
                    ++firstNull;

                if (firstNull == 2)
                    return Empty;

                byte[] buf = new byte[firstNull - 2];
                for (int i = 2; i < firstNull; ++i)
                    buf[i - 2] = socketAddress[i];

                return new UnixEndpoint(Encoding.UTF8.GetString(buf));
            }

            public override string ToString()
            {
                return "unix:" + Address;
            }
        }
    }
}

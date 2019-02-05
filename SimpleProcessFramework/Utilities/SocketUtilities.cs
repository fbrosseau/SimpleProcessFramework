using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Spfx.Utilities
{
    internal static class SocketUtilities
    {
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

        public static EndPoint CreateUnixEndpoint(string addr)
        {
            Type t = Type.GetType("System.Net.Sockets.UnixDomainSocketEndPoint, System.Net.Sockets");
            if (t != null)
                return (EndPoint)Activator.CreateInstance(t, new [] { addr });

            return new UnixEndpoint(addr);
        }
    }
}

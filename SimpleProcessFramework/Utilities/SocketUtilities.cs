using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SetNotInheritable(Socket sock)
        {
            SetHandleInformation(sock.Handle, HANDLE_FLAGS.INHERIT, 0);
        }
    }
}

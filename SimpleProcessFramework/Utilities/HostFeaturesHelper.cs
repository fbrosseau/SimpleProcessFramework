using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Spfx.Interfaces;

namespace Spfx.Utilities
{
    public static class HostFeaturesHelper
    {
#if WINDOWS_BUILD
        public static bool IsWslSupported => WslUtilities.IsWslSupported;
#else
        public static bool IsWslSupported => false;
        public static bool IsNetFxSupported => false;
#endif

        public static bool IsNetCoreSupported => true;
        public static bool IsNetFxSupported { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static OsKind LocalMachineOsKind { get; } = GetLocalOs();
        public static ProcessKind LocalProcessKind { get; } = GetLocalProcessKind();

        private static ProcessKind GetLocalProcessKind()
        {
            var desc = RuntimeInformation.FrameworkDescription;
            if (desc.StartsWith(".net framework", StringComparison.OrdinalIgnoreCase))
            {
                return Environment.Is64BitProcess ? ProcessKind.Netfx : ProcessKind.Netfx32;
            }

            return Environment.Is64BitProcess ? ProcessKind.Netcore : ProcessKind.Netcore32;
        }

        private static OsKind GetLocalOs()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return OsKind.Windows;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return OsKind.Linux;

            return OsKind.Other;
        }

        public static bool IsProcessKindSupported(ProcessKind processKind)
        {
            switch (processKind)
            {
                case ProcessKind.Wsl:
                    return IsWslSupported;
                case ProcessKind.Netfx:
                case ProcessKind.Netfx32:
                case ProcessKind.AppDomain:
                    return IsNetFxSupported && LocalProcessKind.IsNetfx();
                case ProcessKind.Netcore:
                    return IsNetCoreSupported;
                case ProcessKind.Netcore32:
                    return IsNetCoreSupported && IsNetFxSupported;
                case ProcessKind.DirectlyInRootProcess:
                case ProcessKind.Default:
                    return true;
                default:
                    return false;
            }
        }
    }
}

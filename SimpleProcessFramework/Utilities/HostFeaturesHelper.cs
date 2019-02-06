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

        public static bool IsProcessKindSupported(ProcessKind processKind)
        {
            switch (processKind)
            {
                case ProcessKind.Wsl:
                    return IsWslSupported;
                case ProcessKind.Netfx:
                case ProcessKind.Netfx32:
                case ProcessKind.AppDomain:
                    return IsNetFxSupported && ProcessUtilities.GetCurrentProcessKind().IsNetfx();
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

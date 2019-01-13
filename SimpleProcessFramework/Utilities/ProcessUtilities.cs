using SimpleProcessFramework.Interfaces;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SimpleProcessFramework.Utilities
{
    internal static class ProcessUtilities
    {
        public static bool TryKill(this Process proc)
        {
            try
            {
                if (!proc.HasExited)
                    proc.Kill();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static ProcessKind GetCurrentProcessKind()
        {
            var desc = RuntimeInformation.FrameworkDescription;
            if (desc.StartsWith(".net framework", StringComparison.OrdinalIgnoreCase))
            {
                return Environment.Is64BitProcess ? ProcessKind.Netfx : ProcessKind.Netfx32;
            }

            return Environment.Is64BitProcess ? ProcessKind.Netcore : ProcessKind.Netcore32;
        }
    }
}

using System;
using System.IO;
using System.Net.Sockets;

namespace Spfx.Utilities
{
    internal static class WslUtilities
    {
        private static readonly Lazy<bool> s_isWslSupported = new Lazy<bool>(() =>
        {
            try
            {
                using (new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP))
                {
                    // this will throw
                }

                return new FileInfo(WslExeFullPath).Exists;
            }
            catch
            {
                return false;
            }
        }, false);

        public static readonly string WslExeFullPath = Path.Combine(PathHelper.RealSystem32Folder, "wsl.exe");

        public static bool IsWslSupported => s_isWslSupported.Value;

        internal static string GetLinuxPath(string fullName)
        {
            if (fullName.EndsWith("\\"))
                fullName = fullName.Substring(0, fullName.Length - 1);

            var cmd = $"{ProcessUtilities.EscapeArg(WslExeFullPath)} wslpath {ProcessUtilities.EscapeArg(Path.GetFullPath(fullName))}";
            var output = ProcessUtilities.ExecAndGetConsoleOutput(cmd, TimeSpan.FromSeconds(30));
            output = output.Trim(' ', '\r', '\t', '\n');
            if (!output.EndsWith("/", StringComparison.OrdinalIgnoreCase))
                output += '/';
            return output;
        }
    }
}

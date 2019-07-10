using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

namespace Spfx.Utilities
{
    internal static class WslUtilities
    {
        private static string[] s_installedRuntimesInWsl;

        private static readonly Lazy<bool> s_isWslSupported = new Lazy<bool>(() =>
        {
            bool tryCleanupFile = false;
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            try
            {
                using (var s = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
                {
                    s.Bind(SocketUtilities.CreateUnixEndpoint(tempFile));
                    tryCleanupFile = true;
                    // this will throw when not supported
                }

                return new FileInfo(WslExeFullPath).Exists;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (tryCleanupFile)
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch
                    {
                        // it's just best effort, we're using %tmp% anyway.
                    }
                }
            }
        }, false);

        public static readonly string WslExeFullPath = Path.Combine(PathHelper.RealSystem32Folder, "wsl.exe");

        public static bool IsWslSupported => s_isWslSupported.Value;

        internal static IReadOnlyList<string> GetInstalledNetcoreRuntimesInWsl()
        {
            if (s_installedRuntimesInWsl != null)
                return s_installedRuntimesInWsl;

            var cmdOutput = ExecuteWslExe("dotnet " + NetcoreHelper.ListRuntimesCommand);
            s_installedRuntimesInWsl = NetcoreHelper.ParseNetcoreRuntimes(cmdOutput);
            return s_installedRuntimesInWsl;
        }

        internal static string GetLinuxPath(string fullName)
        {
            if (fullName.EndsWith("\\"))
                fullName = fullName.Substring(0, fullName.Length - 1);

            var output = ExecuteWslExe($"wslpath {ProcessUtilities.FormatArgument(Path.GetFullPath(fullName))}");

            output = output.Trim(' ', '\r', '\t', '\n');
            if (!output.EndsWith("/", StringComparison.OrdinalIgnoreCase))
                output += '/';
            return output;
        }

        private static string ExecuteWslExe(string linuxCommand)
        {
            var cmd = $"{ProcessUtilities.FormatArgument(WslExeFullPath)} {linuxCommand}";
            return ProcessUtilities.ExecAndGetConsoleOutput(cmd, TimeSpan.FromSeconds(30)).Result;
        }
    }
}

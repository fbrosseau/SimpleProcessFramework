using System;
using System.IO;
using System.Net.Sockets;

namespace Spfx.Utilities.Runtime
{
    internal static class WslUtilities
    {
        private static ThreadSafeAppendOnlyDictionary<string, string> s_windowsToLinuxPathMappings = new ThreadSafeAppendOnlyDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static readonly string WslExeFullPath = Path.Combine(PathHelper.RealSystem32Folder, "wsl.exe");

        public static bool IsWslSupported => NetcoreHelper.IsSupported;

        public static NetcoreInfo NetcoreHelper { get; } = new WslNetcoreInfo();

        internal static string GetCachedLinuxPath(string windowsName)
        {
            if (s_windowsToLinuxPathMappings.TryGetValue(windowsName, out var linuxPath))
                return linuxPath;

            linuxPath = GetLinuxPath(windowsName);
            s_windowsToLinuxPathMappings[windowsName] = linuxPath;
            return linuxPath;
        }

        internal static string GetLinuxPath(string fullName)
        {
            bool isDir = fullName.EndsWith("\\");

            var output = ExecuteWslExe($"wslpath {ProcessUtilities.FormatCommandLineArgument(Path.GetFullPath(fullName))}");

            output = output.Trim(' ', '\r', '\t', '\n');

            if (isDir && !output.EndsWith("/"))
                output += '/';

            return output;
        }

        private static string ExecuteWslExe(string linuxCommand)
        {
            return ProcessUtilities.ExecAndGetConsoleOutput(WslExeFullPath, linuxCommand, TimeSpan.FromSeconds(30)).Result;
        }

        private class WslNetcoreInfo : NetcoreInfo
        {
            public WslNetcoreInfo()
                : base(() => "dotnet")
            {
            }

            protected override bool CheckIsSupported(out string reason)
            {
                if (!HostFeaturesHelper.IsWindows)
                {
                    reason = "Only supported on Windows hosts";
                    return false;
                }

                if (!new FileInfo(WslExeFullPath).Exists)
                {
                    reason = "WSL.exe could not be found";
                    return false;
                }


                bool tryCleanupFile = false;
                var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

                try
                {
                    using (var s = SocketUtilities.CreateSocket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
                    {
                        s.Bind(new UnixDomainSocketEndPoint(tempFile));
                        tryCleanupFile = true;
                        // this will throw when not supported
                    }

                    reason = null;
                    return true;
                }
                catch
                {
                    reason = "AF_UNIX Socket test failed";
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
            }

            internal override string RunDotNetExe(string command)
            {
                return ExecuteWslExe(
                    $"{ProcessUtilities.FormatCommandLineArgument(NetCoreHostPath)} {command}");
            }
        }
    }
}

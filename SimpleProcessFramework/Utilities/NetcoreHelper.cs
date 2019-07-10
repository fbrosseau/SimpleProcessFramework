using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Spfx.Utilities
{
    internal static class NetcoreHelper
    {
        private static string[] s_installedNetcoreRuntimes;
        private static string[] s_installedNetcore32Runtimes;
        internal static readonly string ListRuntimesCommand = "--list-runtimes";

        public static Version NetcoreVersion => s_netcoreVersion.Value;

        internal static bool NetCoreExists(bool anyCpu)
        {
            return (anyCpu && !HostFeaturesHelper.IsWindows) || new FileInfo(GetNetCoreHostPath(anyCpu)).Exists;
        }

        public static string[] GetInstalledNetcoreRuntimes(bool anyCpu = true)
        {
            return (string[])GetInstalledNetcoreRuntimesInternal(anyCpu).Clone();
        }

        private static string[] GetInstalledNetcoreRuntimesInternal(bool anyCpu)
        {
            if (anyCpu)
                return GetInstalledNetcoreRuntimes(true, ref s_installedNetcoreRuntimes);
            return GetInstalledNetcoreRuntimes(false, ref s_installedNetcore32Runtimes);
        }

        private static string[] GetInstalledNetcoreRuntimes(bool anyCpu, ref string[] cachedResult)
        {
            var runtimes = cachedResult;
            if (runtimes != null)
                return runtimes;

            var versions = ProcessUtilities.ExecAndGetConsoleOutput(GetNetCoreHostPath(anyCpu), ListRuntimesCommand, TimeSpan.FromSeconds(30)).Result;
            cachedResult = ParseNetcoreRuntimes(versions);
            return cachedResult;
        }

        internal static string[] ParseNetcoreRuntimes(string listRuntimesProgramOutput)
        {
            var lines = listRuntimesProgramOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var regex = new Regex(@"\s*Microsoft\.NETCore\.App\s+(?<ver>[a-z0-9.-]+)\s*\[.*?\]", RegexOptions.IgnoreCase);
            string ParseLine(string l)
            {
                var m = regex.Match(l);
                if (!m.Success)
                    return null;
                return m.Groups["ver"].Value;
            }

            return lines.Select(ParseLine).Where(l => l != null).OrderByDescending(l => l, LexicographicStringComparer.Instance).ToArray();
        }

        public static string GetBestNetcoreRuntime(string requestedVersion, bool anyCpu = true)
        {
            var choices = GetInstalledNetcoreRuntimesInternal(anyCpu);
            return choices.FirstOrDefault(runtime => runtime.StartsWith(requestedVersion, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetNetCoreHostPath(bool anyCpu)
        {
            if (!HostFeaturesHelper.IsWindows)
                return "dotnet";

            var suffix = "dotnet\\dotnet.exe";
            if (!anyCpu && Environment.Is64BitOperatingSystem)
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), suffix);

            return Path.Combine(Environment.ExpandEnvironmentVariables("%ProgramW6432%"), suffix);
        }

        private static readonly Lazy<Version> s_netcoreVersion = new Lazy<Version>(() =>
        {
            var m = Regex.Match(RuntimeInformation.FrameworkDescription, @"\.NET Core (?<v>[\d\.]+)", RegexOptions.IgnoreCase);
            if (!m.Success)
                throw new InvalidOperationException("Could not determine version");

            var ver = ParseNetcoreVersion(m.Groups["v"].Value);
            if (ver.Major == 4)
            {
                if (ver.Minor == 6)
                    return new Version(2, 1, 0);
                throw new InvalidOperationException("Could not determine version");
            }

            return ver;
        });

        internal static Version ParseNetcoreVersion(string value)
        {
            Guard.ArgumentNotNullOrEmpty(value, nameof(value));
            if (!value.Contains("."))
                return new Version(int.Parse(value), 0);

            return new Version(value);
        }
    }
}

using Spfx.Interfaces;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace Spfx.Utilities
{
    internal static class NetcoreHelper
    {
        public static class WellKnownArguments
        {
            public const string FrameworkVersion = "--fx-version";
            public const string ListRuntimesCommand = "--list-runtimes";
        }

        private static readonly Regex s_runtimeDescriptionRegex = new Regex(@"\s*Microsoft\.NETCore\.App\s+(?<fullName>(?<numbers>[0-9.]+)[a-z0-9.-]*)\s*\[.*?\]", RegexOptions.IgnoreCase);
        private static readonly Regex s_runtimeVersionRegex = new Regex(@"^(?<ver>[0-9.]+)[a-z0-9.-]*$", RegexOptions.IgnoreCase);
        private static readonly Lazy<Version> s_netcoreVersion = new Lazy<Version>(GetCurrentNetcoreVersion);

        private static readonly Lazy<string[]> s_installedNetcoreRuntimes = new Lazy<string[]>(() => GetInstalledNetcoreRuntimesInternal(true), LazyThreadSafetyMode.PublicationOnly);
        private static readonly Lazy<string[]> s_installedNetcore32Runtimes = new Lazy<string[]>(() => GetInstalledNetcoreRuntimesInternal(false), LazyThreadSafetyMode.PublicationOnly);

        public static Version NetcoreVersion => s_netcoreVersion.Value;

        public static string NetcoreFrameworkVersion { get; internal set; }

        internal static bool NetCoreExists(bool anyCpu)
        {
            return (anyCpu && !HostFeaturesHelper.IsWindows) || new FileInfo(GetNetCoreHostPath(anyCpu)).Exists;
        }

        internal static bool IsNetcoreAtLeastVersion(int major, int minor = -1)
        {
            if (!HostFeaturesHelper.LocalProcessKind.IsNetcore())
                return false;

            return NetcoreVersion.Major >= major && NetcoreVersion.Minor >= minor;
        }

        public static string[] GetInstalledNetcoreRuntimes(bool anyCpu = true)
        {
            if (anyCpu)
                return (string[])s_installedNetcoreRuntimes.Value.Clone();
            return (string[])s_installedNetcore32Runtimes.Value.Clone();
        }

        private static string[] GetInstalledNetcoreRuntimesInternal(bool anyCpu)
        {
            var versions = RunDotNetExe(anyCpu, WellKnownArguments.ListRuntimesCommand);
            return ParseNetcoreRuntimes(versions);
        }

        internal static string RunDotNetExe(bool anyCpu, string command)
        {
            return ProcessUtilities.ExecAndGetConsoleOutput(GetNetCoreHostPath(anyCpu), command, TimeSpan.FromSeconds(30)).Result;
        }

        internal static string[] ParseNetcoreRuntimes(string listRuntimesProgramOutput)
        {
            var lines = listRuntimesProgramOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines
                .Select(ParseRuntimeDescription).Where(l => l != null)
                .OrderByDescending(l => l, LexicographicStringComparer.Instance)
                .ToArray();
        }

        internal static string ParseRuntimeDescription(string runtimeDescription)
        {
            var m = s_runtimeDescriptionRegex.Match(runtimeDescription);
            if (!m.Success)
                return null;
            return m.Groups["fullName"].Value;
        }

        internal static Version ParseRuntimeVersionNumber(string runtimeName)
        {
            var m = s_runtimeVersionRegex.Match(runtimeName);
            if (!m.Success)
                return null;
            return ParseNetcoreVersion(m.Groups["ver"].Value);
        }

        public static string GetBestNetcoreRuntime(string requestedVersion, ProcessKind processKind = ProcessKind.Netcore)
        {
            if (!processKind.IsNetcore())
                throw new ArgumentException(processKind + " is not a valid .Net Core process kind");

            bool anyCpu = !processKind.Is32Bit() || !Environment.Is64BitOperatingSystem;
            bool wsl = processKind == ProcessKind.Wsl;

            if (requestedVersion is null)
                requestedVersion = "";

            var choices = wsl ?
                WslUtilities.GetInstalledNetcoreRuntimesInWsl()
                : GetInstalledNetcoreRuntimesInternal(anyCpu);

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

        private static Version GetCurrentNetcoreVersion()
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
        }

        internal static Version ParseNetcoreVersion(string value)
        {
            Guard.ArgumentNotNullOrEmpty(value, nameof(value));
            if (!value.Contains("."))
                return new Version(int.Parse(value), 0);

            return new Version(value);
        }

        internal static string GetDefaultNetcoreBinSubfolderName(string runtime)
        {
            var version = ParseRuntimeVersionNumber(runtime);
            return $"netcoreapp{version.Major}.{version.Minor}";
        }
    }
}

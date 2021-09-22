using Spfx.Interfaces;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities.Runtime
{
    internal class NetcoreInfo
    {
        private static readonly Regex s_runtimeDescriptionRegex = new Regex(@"\s*Microsoft\.NETCore\.App\s+(?<fullName>(?<numbers>[0-9.]+)[a-z0-9.-]*)\s*\[.*?\]", RegexOptions.IgnoreCase);
        private static readonly Regex s_runtimeVersionRegex = new Regex(@"^(?<ver>[0-9.]+)[a-z0-9.-]*$", RegexOptions.IgnoreCase);
        private static readonly Lazy<Version> s_processNetcoreVersion = new Lazy<Version>(GetCurrentNetcoreVersion);

        public static Version CurrentProcessNetcoreVersion => s_processNetcoreVersion.Value;
        public static string NetcoreFrameworkVersion { get; internal set; }

        public static class WellKnownArguments
        {
            public const string FrameworkVersion = "--fx-version";
            public const string ListRuntimesCommand = "--list-runtimes";
            public const string VersionCommand = "--version";
        }

        public static NetcoreInfo Default { get; } = new NetcoreInfo(() => GetDefaultDotNetExePath(true));
        public static NetcoreInfo X86 { get; } = new NetcoreInfo(() => GetDefaultDotNetExePath(false));
        public static NetcoreInfo Wsl => WslUtilities.NetcoreHelper;

        private readonly AsyncLazy<string[]> m_installedNetcoreRuntimes;
        private readonly Lazy<string> m_dotnetExePath;
        private readonly AsyncLazy<string> m_dotnetExeVersion;
        private readonly Lazy<bool> m_isInstalled;

        protected NetcoreInfo(Func<string> dotnetPath)
        {
            static Lazy<T> Lazy<T>(Func<T> func)
                => new Lazy<T>(func, LazyThreadSafetyMode.PublicationOnly);

            m_dotnetExePath = Lazy(dotnetPath);
            m_isInstalled = Lazy(CheckIsSupported);
            m_dotnetExeVersion = AsyncLazy.Create(() => RunDotNetExeAsync(WellKnownArguments.VersionCommand));
            m_installedNetcoreRuntimes = AsyncLazy.Create(GetInstalledNetcoreRuntimesInternal);
        }

        public string NetCoreHostPath => m_dotnetExePath.Value;
        public IReadOnlyList<string> InstalledVersions => m_installedNetcoreRuntimes.SynchronousResult;
        public Task<string[]> GetInstalledVersionsAsync() => m_installedNetcoreRuntimes.ResultTask;
        public string DotNetExeVersion => m_dotnetExeVersion.SynchronousResult;
        public Task<string> GetDotNetExeVersionAsync => m_dotnetExeVersion.ResultTask;
        public bool IsSupported => m_isInstalled.Value;
        public string NotSupportedReason { get; private set; }

        internal static bool NetCoreExists(bool anyCpu)
        {
            return GetHelper(anyCpu).IsSupported;
        }

        internal static bool IsCurrentProcessAtLeastNetcoreVersion(int major, int minor = -1)
        {
            if (!HostFeaturesHelper.LocalProcessIsNetcore)
                return false;
            return CurrentProcessNetcoreVersion.Major >= major && CurrentProcessNetcoreVersion.Minor >= minor;
        }

        private async Task<string[]> GetInstalledNetcoreRuntimesInternal()
        {
            var versions = await RunDotNetExeAsync(WellKnownArguments.ListRuntimesCommand).ConfigureAwait(false);
            return ParseNetcoreRuntimes(versions);
        }

        internal static Task<string> RunDotNetExeAsync(bool anyCpu, string command)
        {
            return GetHelper(anyCpu).RunDotNetExeAsync(command);
        }

        internal virtual Task<string> RunDotNetExeAsync(string command)
        {
            return ProcessUtilities.ExecAndGetConsoleOutput(NetCoreHostPath, command, TimeSpan.FromSeconds(30));
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

        private bool CheckIsSupported()
        {
            try
            {
                if (!CheckIsSupported(out var reason))
                {
                    NotSupportedReason = reason;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                NotSupportedReason = ex.ToString();
                return false;
            }
        }

        protected virtual bool CheckIsSupported(out string reason)
        {
            reason = null;

            if (!HostFeaturesHelper.IsWindows)
                return true;

            try
            {
                reason = "Invalid dotnet path";
                var path = NetCoreHostPath;
                var fi = new FileInfo(path);
                reason = "'dotnet' does not exist: " + path;
                return fi.Exists;
            }
            catch
            {
                return false;
            }
        }

        public static IReadOnlyList<string> GetSupportedRuntimes(ProcessKind processKind)
        {
            return GetHelper(processKind).InstalledVersions;
        }

        public static string GetBestNetcoreRuntime(NetcoreTargetFramework fw)
        {
            return GetBestNetcoreRuntime(fw.TargetRuntime, fw.ProcessKind);
        }

        public static string GetBestNetcoreRuntime(string requestedVersion, ProcessKind processKind)
        {
            if (!processKind.IsNetcore())
                throw new ArgumentException(processKind + " is not a valid .Net Core process kind");

            return GetHelper(processKind).GetBestNetcoreRuntime(requestedVersion);
        }

        public string GetBestNetcoreRuntime(string requestedVersion)
        {
            if (requestedVersion is null)
                requestedVersion = "";

            var choices = InstalledVersions;
            return choices.FirstOrDefault(runtime => runtime.StartsWith(requestedVersion, StringComparison.OrdinalIgnoreCase));
        }

        private static NetcoreInfo GetHelper(bool anyCpu)
            => anyCpu ? Default : X86;
        private static NetcoreInfo GetHelper(ProcessKind kind)
            => kind == ProcessKind.Wsl ? Wsl : GetHelper(!HostFeaturesHelper.IsWindows || !kind.Is32Bit());

        public static string GetNetCoreHostPath(bool anyCpu)
        {
            return GetHelper(anyCpu).NetCoreHostPath;
        }

        private static string GetDefaultDotNetExePath(bool anyCpu)
        {
            var variable = anyCpu ? "SPFX_DOTNET_EXE_PATH" : "SPFX_DOTNET_EXE_PATH32";
            var env = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrEmpty(env))
                return env;

            if (!HostFeaturesHelper.IsWindows)
                return "dotnet";

            var suffix = "dotnet\\dotnet.exe";
            if (!anyCpu && Environment.Is64BitOperatingSystem)
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), suffix);

            return Path.Combine(Environment.ExpandEnvironmentVariables("%ProgramW6432%"), suffix);
        }

        private static Version GetCurrentNetcoreVersion()
        {
            var m = Regex.Match(RuntimeInformation.FrameworkDescription, @"^\.NET (?<v>[\d\.]+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                return new Version(m.Groups["v"].Value);
            }

#if NETCORE31_SUPPORTED
            m = Regex.Match(RuntimeInformation.FrameworkDescription, @"^\.NET Core (?<v>[\d\.]+)", RegexOptions.IgnoreCase);
            if (!m.Success)
                throw new InvalidDataException("Could not determine version: " + RuntimeInformation.FrameworkDescription);

            var ver = ParseNetcoreVersion(m.Groups["v"].Value);
            if (ver.Major == 4)
            {
                if (ver.Minor == 6)
                    return new Version(2, 1, 0);
                throw new InvalidDataException("Could not determine version");
            }
#endif

            return ver;
        }

        internal static Version ParseNetcoreVersion(string value)
        {
            Guard.ArgumentNotNullOrEmpty(value, nameof(value));
            if (!value.Contains('.'))
                return new Version(int.Parse(value), 0);

            return new Version(value);
        }

        internal static string GetDefaultNetcoreBinSubfolderName(string runtime)
        {
            var version = ParseRuntimeVersionNumber(runtime);

#if NETCORE31_SUPPORTED
            if (version.Major < 5)
                return $"netcoreapp{version.Major}.{version.Minor}";
#endif

            return $"net{version.Major}.{version.Minor}";
        }

        public static Task InitializeInstalledVersionsAsync(bool x86 = false)
        {
            return (x86 ? X86 : Default).GetInstalledVersionsAsync();
        }
    }
}

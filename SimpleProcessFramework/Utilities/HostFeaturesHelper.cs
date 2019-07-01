using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Spfx.Interfaces;

namespace Spfx.Utilities
{
    public static class HostFeaturesHelper
    {
        private static string[] s_installedNetcoreRuntimes;
        private static string[] s_installedNetcore32Runtimes;

        public static bool IsWindows => LocalMachineOsKind == OsKind.Windows;
        public static bool IsWslSupported => WslUtilities.IsWslSupported;
        public static bool Is32BitSupported => IsWindows;

        public static bool IsNetCoreSupported { get; } = !IsWindows || NetCoreExists(true);
        public static bool IsNetCore32Supported { get; } = IsWindows && NetCoreExists(false);

        public static bool IsAppDomainSupported => LocalProcessKind.IsNetfx();

        private static readonly Lazy<bool> s_isInsideWsl = new Lazy<bool>(() => LocalMachineOsKind == OsKind.Linux && File.ReadAllText("/proc/sys/kernel/osrelease").IndexOf("microsoft", StringComparison.OrdinalIgnoreCase) != -1);
        public static bool IsInsideWsl { get; } = !IsWindows && s_isInsideWsl.Value;

        public static OsKind LocalMachineOsKind { get; } = GetLocalOs();
        public static bool IsNetFxSupported { get; } = IsWindows;
        public static ProcessKind LocalProcessKind { get; } = GetLocalProcessKind();
        public static string CurrentProcessRuntimeDescription { get; } = GetRuntimeDescription();
        public static Version NetcoreVersion => s_netcoreVersion.Value;

#if DEBUG
        public const bool IsDebugBuild = true;
#else
        public const bool IsDebugBuild = false;
#endif

        public static string GetNetCoreHostPath(bool anyCpu)
        {
            if (!IsWindows)
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

            var ver = new Version(m.Groups["v"].Value);
            if(ver.Major == 4)
            {
                if (ver.Minor == 6)
                    return new Version(2, 1, 0);
                throw new InvalidOperationException("Could not determine version");
            }

            return ver;
        });

        private static bool NetCoreExists(bool anyCpu)
        {
            return new FileInfo(GetNetCoreHostPath(anyCpu)).Exists;
        }

        private static string GetRuntimeDescription()
        {
            if (LocalProcessKind.IsNetfx())
            {
                var currentDomain = AppDomain.CurrentDomain;
                var setupInfo = typeof(AppDomain).GetProperty("SetupInformation").GetValue(currentDomain);
                return setupInfo.GetType().GetProperty("TargetFrameworkName").GetValue(setupInfo)?.ToString();
            }
            else
            {
                return RuntimeInformation.FrameworkDescription;
            }
        }

        public static string[] GetInstalledNetcoreRuntimes(bool anyCpu = true)
        {
            return (string[])GetInstalledNetcoreRuntimesInternal(anyCpu).Clone();
        }

        private static string[] GetInstalledNetcoreRuntimesInternal(bool anyCpu)
        {
            if (anyCpu)
                return GetInstalledNetcoreRuntimes(anyCpu, ref s_installedNetcoreRuntimes);
            return GetInstalledNetcoreRuntimes(anyCpu, ref s_installedNetcore32Runtimes);
        }

        private static string[] GetInstalledNetcoreRuntimes(bool anyCpu, ref string[] cachedResult)
        {
            var runtimes = cachedResult;
            if (runtimes != null)
                return runtimes;

            var cmdLine = $"{ProcessUtilities.EscapeArg(GetNetCoreHostPath(anyCpu))} \"--list-runtimes\"";
            var versions = ProcessUtilities.ExecAndGetConsoleOutput(cmdLine, TimeSpan.FromSeconds(30));
            var lines = versions.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var regex = new Regex(@"\s*Microsoft\.NETCore\.App\s+(?<ver>[a-z0-9.-]+)\s*\[.*?\]", RegexOptions.IgnoreCase);
            string ParseLine(string l)
            {
                var m = regex.Match(l);
                if (!m.Success)
                    return null;
                return m.Groups["ver"].Value;
            }

            cachedResult = lines.Select(ParseLine).Where(l => l != null).OrderByDescending(l => l, LexicographicStringComparer.Instance).ToArray();
            return cachedResult;
        }

        public static string GetBestNetcoreRuntime(string requestedVersion, bool anyCpu = true)
        {
            var choices = GetInstalledNetcoreRuntimesInternal(anyCpu);
            return choices.FirstOrDefault(runtime => runtime.StartsWith(requestedVersion, StringComparison.OrdinalIgnoreCase));
        }

        private static ProcessKind GetLocalProcessKind()
        {
            if (!IsWindows)
            {
                // only distinguish 32 vs 64 on Windows
                return IsInsideWsl ? ProcessKind.Wsl : ProcessKind.Netcore;
            }
            else
            {
                var desc = RuntimeInformation.FrameworkDescription;
                if (desc.StartsWith(".net framework", StringComparison.OrdinalIgnoreCase))
                {
                    return Environment.Is64BitProcess ? ProcessKind.Netfx : ProcessKind.Netfx32;
                }

                return Environment.Is64BitProcess ? ProcessKind.Netcore : ProcessKind.Netcore32;
            }
        }

        private static OsKind GetLocalOs()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return OsKind.Windows;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return OsKind.Linux;

            return OsKind.Other;
        }

        public static bool IsProcessKindSupportedByCurrentProcess(ProcessKind kind)
        {
            return IsProcessKindSupportedByCurrentProcess(kind, out _);
        }

        public static bool IsProcessKindSupportedByCurrentProcess(ProcessKind kind, out string details)
        {
            if (kind.IsNetfx() && !IsWindows)
            {
                details = ".Net Framework is only supported on Windows";
                return false;
            }

            if (kind == ProcessKind.AppDomain && !IsAppDomainSupported)
            {
                details = "AppDomains can only be used in .Net Framework hosts";
                return false;
            }

            if (kind.Is32Bit() && !Is32BitSupported)
            {
                details = "Only Windows supports 32-bit processes";
                return false;
            }

            if (kind == ProcessKind.Wsl && !IsWslSupported)
            {
                details = "WSL is not supported on this platform";
                return false;
            }

            if (kind == ProcessKind.Netcore && !IsNetCoreSupported)
            {
                details = ".Net core is not supported on this host";
                return false;
            }

            if (kind == ProcessKind.Netcore32 && !IsNetCore32Supported)
            {
                details = "32-bit .Net core is not supported on this host";
                return false;
            }

            details = null;
            return true;
        }

        internal static ProcessKind GetBestAvailableProcessKind(ProcessKind processKind, ProcessClusterConfiguration config)
        {
            if (IsProcessKindSupportedByCurrentProcess(processKind, config, out string originalError))
                return processKind;

            if (!config.FallbackToBestAvailableProcessKind)
                throw new PlatformNotSupportedException($"ProcessKind {processKind} is not supported: {originalError}");

            if (processKind == ProcessKind.AppDomain && config.EnableFakeProcesses)
                return ProcessKind.DirectlyInRootProcess;

            if (processKind.IsFakeProcess())
                throw new PlatformNotSupportedException($"ProcessKind {processKind} is not supported: {originalError}");

            if (processKind.Is32Bit())
            {
                var anyCpu = processKind.AsAnyCpu();
                if (IsProcessKindSupportedByCurrentProcess(anyCpu, config, out _))
                    return anyCpu;
            }

            if(processKind.IsNetcore() && IsNetFxSupported)
            {
                if (IsProcessKindSupportedByCurrentProcess(ProcessKind.Netfx, config, out _))
                    return ProcessKind.Netfx;
            }

            if (processKind.IsNetfx() && IsNetCoreSupported)
            {
                if (IsNetCoreSupported && IsProcessKindSupportedByCurrentProcess(ProcessKind.Netcore, config, out _))
                    return ProcessKind.Netcore;
                if (IsNetCore32Supported && IsProcessKindSupportedByCurrentProcess(ProcessKind.Netcore32, config, out _))
                    return ProcessKind.Netcore32;
            }

            throw new PlatformNotSupportedException($"ProcessKind {processKind} is not supported: {originalError}");
        }

        public static bool IsProcessKindSupportedByCurrentProcess(ProcessKind processKind, ProcessClusterConfiguration config, out string error)
        {
            if (!IsProcessKindSupportedByCurrentProcess(processKind, out error))
                return false;

            if (processKind.Is32Bit() && !config.Enable32Bit)
            {
                error = "This current configuration does not support 32-bit processes";
                return false;
            }

            if (processKind.IsNetfx() && !config.EnableNetfx)
            {
                error = "This current configuration does not support .Net Framework";
                return false;
            }

            if (processKind.IsNetcore() && !config.EnableNetcore)
            {
                error = "This current configuration does not support .Net Core";
                return false;
            }

            if (processKind == ProcessKind.AppDomain && !config.EnableAppDomains)
            {
                error = "This current configuration does not support AppDomains";
                return false;
            }

            if(processKind == ProcessKind.Wsl && !config.EnableWsl)
            {
                error = "This current configuration does not support WSL";
                return false;
            }

            return true;
        }
    }
}
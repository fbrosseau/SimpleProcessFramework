using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Spfx.Interfaces;

namespace Spfx.Utilities
{
    public static class HostFeaturesHelper
    {
        private static string[] s_installedNetcoreRuntimes;
        private static string[] s_installedNetcore32Runtimes;

#if WINDOWS_BUILD
        public static bool IsWslSupported => WslUtilities.IsWslSupported;
        public static bool IsNetCoreSupported { get; } = NetCoreExists(true);
        public static bool IsNetCore32Supported { get; } = NetCoreExists(false);
        public static bool IsInsideWsl => false;

        public static string GetNetCoreHostPath(bool anyCpu)
        {
            var suffix = "dotnet\\dotnet.exe";
            if (!anyCpu && Environment.Is64BitOperatingSystem)
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), suffix);

            return Path.Combine(Environment.ExpandEnvironmentVariables("%ProgramW6432%"), suffix);
        }

        private static bool NetCoreExists(bool anyCpu)
        {
            return new FileInfo(GetNetCoreHostPath(anyCpu)).Exists;
        }
#else
        private static readonly Lazy<bool> s_isInsideWsl = new Lazy<bool>(() => LocalMachineOsKind == OsKind.Linux && File.ReadAllText("/proc/sys/kernel/osrelease").IndexOf("microsoft", StringComparison.OrdinalIgnoreCase) != -1);
        public static bool IsInsideWsl => s_isInsideWsl.Value;

        public static bool IsWslSupported => false;
        public static bool IsNetCoreSupported => true;
        public static bool IsNetCore32Supported => false;

        public static string GetNetCoreHostPath(bool anyCpu)
        {
            if (!anyCpu)
                throw new ArgumentException("Non-Windows is only AnyCPU");
            return "dotnet";
        }
#endif

        public static bool IsNetFxSupported { get; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static OsKind LocalMachineOsKind { get; } = GetLocalOs();
        public static ProcessKind LocalProcessKind { get; } = GetLocalProcessKind();
        public static string CurrentProcessRuntimeDescription { get; } = GetRuntimeDescription();

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
                return Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
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

            cachedResult = lines.Select(ParseLine).Where(l => l != null).OrderByDescending(l => l, StringComparer.OrdinalIgnoreCase).ToArray();
            return cachedResult;
        }

        public static string GetBestNetcoreRuntime(string requestedVersion, bool anyCpu = true)
        {
            var choices = GetInstalledNetcoreRuntimesInternal(anyCpu);
            return choices.FirstOrDefault(runtime => runtime.StartsWith(requestedVersion, StringComparison.OrdinalIgnoreCase));
        }

        private static ProcessKind GetLocalProcessKind()
        {
            if (GetLocalOs() == OsKind.Linux)
            {
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

        public static bool IsProcessKindSupported(ProcessKind processKind)
        {
            switch (processKind)
            {
                case ProcessKind.Wsl:
                    return IsWslSupported;
                case ProcessKind.Netfx:
                case ProcessKind.Netfx32:
                    return IsNetFxSupported;
                case ProcessKind.AppDomain:
                    return IsNetFxSupported && LocalProcessKind.IsNetfx();
                case ProcessKind.Netcore:
                    return IsNetCoreSupported;
                case ProcessKind.Netcore32:
                    return IsNetCore32Supported;
                case ProcessKind.DirectlyInRootProcess:
                case ProcessKind.Default:
                    return true;
                default:
                    return false;
            }
        }
    }
}
using Spfx.Interfaces;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Spfx.Utilities.Runtime
{
    public static class HostFeaturesHelper
    {
        public static OsKind LocalMachineOsKind { get; } = GetLocalOs();
        public static ProcessKind LocalProcessKind { get; } = GetLocalProcessKind();
        public static string CurrentProcessRuntimeDescription { get; } = GetRuntimeDescription();

        public static bool IsWindows => LocalMachineOsKind == OsKind.Windows;
        public static bool IsWslSupported => WslUtilities.IsWslSupported;
        public static bool Is32BitSupported => IsWindows;

        public static bool IsNetCoreSupported => !IsWindows || NetcoreInfo.NetCoreExists(true);
        public static bool IsNetCore32Supported => IsWindows && NetcoreInfo.NetCoreExists(false);

        public static bool IsAppDomainSupported => LocalProcessKind.IsNetfxProcess();

        private static readonly Lazy<bool> s_isInsideWsl = new Lazy<bool>(() => LocalMachineOsKind == OsKind.Linux && File.ReadAllText("/proc/sys/kernel/osrelease").IndexOf("microsoft", StringComparison.OrdinalIgnoreCase) != -1);
        public static bool IsInsideWsl { get; } = !IsWindows && s_isInsideWsl.Value;

        public static bool IsNetFxSupported { get; } = IsWindows;

        public static bool LocalProcessIsNetfx => LocalProcessKind.IsNetfx();
        public static bool LocalProcessIsNetcore => LocalProcessKind.IsNetcore();

#if DEBUG
        public static readonly bool IsDebugBuildConstant = true;
#else
        public static readonly bool IsDebugBuildConstant = false;
#endif

        public static readonly bool IsDebugBuild = IsDebugBuildConstant;

        private static string GetRuntimeDescription()
        {
            if (LocalProcessIsNetfx)
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
            if (kind.IsNetfxProcess() && !IsNetFxSupported)
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

            if (kind.IsNetcore() && !IsNetCoreSupported)
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

            if (processKind.IsNetfxProcess() || processKind == ProcessKind.Wsl && IsNetCoreSupported)
            {
                if (IsNetCoreSupported && IsProcessKindSupportedByCurrentProcess(ProcessKind.Netcore, config, out _))
                    return ProcessKind.Netcore;
                if (IsNetCore32Supported && IsProcessKindSupportedByCurrentProcess(ProcessKind.Netcore32, config, out _))
                    return ProcessKind.Netcore32;
            }

            if (processKind.IsNetcore() && IsNetFxSupported)
            {
                if (IsProcessKindSupportedByCurrentProcess(ProcessKind.Netfx, config, out _))
                    return ProcessKind.Netfx;
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

            if (processKind.IsNetfxProcess() && !config.EnableNetfx)
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

            if (processKind == ProcessKind.Wsl && !config.EnableWsl)
            {
                error = "This current configuration does not support WSL";
                return false;
            }

            return true;
        }

        public static string DescribeHost()
        {
            var sb = new StringBuilder();

            try
            {
                DescribeHostImpl(sb);
            }
            catch (Exception ex)
            {
                sb.AppendLine("DescribeHost failed!");
                sb.AppendFormat("{0}", ex);
            }

            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DescribeHostImpl(StringBuilder sb)
        {
            var ver = FileVersionInfo.GetVersionInfo(typeof(HostFeaturesHelper).Assembly.Location).ProductVersion;

            sb.AppendLine("The SimpleProcessFramework (Spfx) Version " + ver);

            sb.AppendLine("Host information---------");
            sb.AppendLine("OS Kind: " + LocalMachineOsKind);
            sb.AppendLine("Current process: " + LocalProcessKind);
            sb.AppendLine("Runtime: " + CurrentProcessRuntimeDescription);
            sb.AppendLine("Is Debug: " + IsDebugBuild);
            sb.AppendLine("NetFX Supported: " + IsNetFxSupported);
            sb.AppendLine("32-bit Supported: " + Is32BitSupported);

            void WriteNetcore(string name, NetcoreInfo h)
            {
                sb.AppendLine(name + " Supported: " + h.IsSupported);
                sb.AppendLine(name + " Path: " + h.NetCoreHostPath);
                if (h.IsSupported)
                {
                    sb.AppendLine(name + " dotnet version: " + h.DotNetExeVersion);
                    sb.AppendFormat("{0} dotnet runtimes: ({1}) ", name, h.InstalledVersions.Count);
                    sb.AppendJoin(',', h.InstalledVersions);
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine(name + " is not supported: " + h.NotSupportedReason);
                }
            }

            WriteNetcore("Netcore", NetcoreInfo.Default);
            WriteNetcore("Netcore-32", NetcoreInfo.X86);
            WriteNetcore("Netcore-wsl", NetcoreInfo.Wsl);
        }
    }
}
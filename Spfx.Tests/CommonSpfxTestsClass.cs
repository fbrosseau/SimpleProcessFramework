using System.Linq;
using Spfx.Interfaces;
using System;
using System.Collections.Generic;
using Spfx.Utilities.Runtime;

namespace Spfx.Tests
{
    public abstract class CommonSpfxTestsClass : CommonTestClass
    {
        public static readonly ProcessKind DefaultProcessKind = ProcessClusterConfiguration.DefaultDefaultProcessKind;

        public static readonly TargetFramework SimpleIsolationKind = TargetFramework.Create(
            HostFeaturesHelper.IsAppDomainSupported
            ? ProcessKind.AppDomain : ProcessKind.Netcore);

        internal static NetcoreTargetFramework LatestNetcore = NetcoreTargetFramework.Create(ProcessKind.Netcore, "3");
        internal static NetcoreTargetFramework LatestNetcore32 = NetcoreTargetFramework.Create(ProcessKind.Netcore32, LatestNetcore.TargetRuntime);

        internal static readonly NetcoreTargetFramework[] AllNetcore = {
            NetcoreTargetFramework.Create(ProcessKind.Netcore, "2.1"),
            NetcoreTargetFramework.Create(ProcessKind.Netcore, "2.2"),
            NetcoreTargetFramework.Create(ProcessKind.Netcore, "3.0"),
            NetcoreTargetFramework.Create(ProcessKind.Netcore, "3.1")
        };

        internal static readonly TargetFramework[] Netfx_AllArchs = { TargetFramework.Create(ProcessKind.Netfx), TargetFramework.Create(ProcessKind.Netfx32) };

        internal static readonly NetcoreTargetFramework[] AllNetcore_AllArchs
            = !Test32Bit ? AllNetcore : AllNetcore.Concat(AllNetcore.Select(n => NetcoreTargetFramework.Create(ProcessKind.Netcore32, n.TargetRuntime))).ToArray();

        internal static readonly TargetFramework[] Netfx_And_AllNetcore_AllArchs
            = Netfx_AllArchs.Concat(AllNetcore_AllArchs).ToArray();

        internal static readonly TargetFramework[] Simple_Netfx_And_Netcore
            = CleanupFrameworks(new[] { LatestNetcore, TargetFramework.Create(ProcessKind.Netfx) });

        internal static readonly TargetFramework[] Netfx_And_NetcoreLatest_AllArchs = {
            TargetFramework.Create(ProcessKind.Netfx),
            TargetFramework.Create(ProcessKind.Netfx32),
            LatestNetcore,
            LatestNetcore32
        };

        internal static readonly TargetFramework[] AllGenericSupportedFrameworks = GetAllGenericSupportedFrameworks();

        private static TargetFramework[] GetAllGenericSupportedFrameworks()
        {
            var result = new List<TargetFramework>();

            result.Add(NetcoreTargetFramework.Create(ProcessKind.Netcore));

            if (HostFeaturesHelper.IsNetCore32Supported)
                result.Add(NetcoreTargetFramework.Create(ProcessKind.Netcore32));

            if (HostFeaturesHelper.IsNetFxSupported)
            {
                result.Add(TargetFramework.Create(ProcessKind.Netfx));
                result.Add(TargetFramework.Create(ProcessKind.Netfx32));
            }

            if (HostFeaturesHelper.IsWslSupported)
                result.Add(TargetFramework.Create(ProcessKind.Wsl));

            return result.ToArray();
        }

        private static TargetFramework[] CleanupFrameworks(TargetFramework[] targetFrameworks)
        {
            return targetFrameworks.Where(fw =>
            {
                switch (fw.ProcessKind)
                {
                    case ProcessKind.AppDomain:
                        return HostFeaturesHelper.IsAppDomainSupported;
                    case ProcessKind.Netcore:
                        return HostFeaturesHelper.IsNetCoreSupported;
                    case ProcessKind.Netcore32:
                        return HostFeaturesHelper.IsNetCore32Supported;
                    case ProcessKind.Netfx:
                        return HostFeaturesHelper.IsNetFxSupported;
                    case ProcessKind.Netfx32:
                        return HostFeaturesHelper.IsNetFxSupported && HostFeaturesHelper.Is32BitSupported;
                    case ProcessKind.Wsl:
                        return HostFeaturesHelper.IsWslSupported;
                    default:
                        return false;
                }
            }).ToArray();
        }

        internal static readonly TargetFramework[] Netfx_And_NetcoreLatest = Netfx_And_NetcoreLatest_AllArchs.Where(f => !f.ProcessKind.Is32Bit()).ToArray();
        internal static readonly TargetFramework[] Netfx_And_Netcore3Plus_AllArchs = Netfx_AllArchs.Concat(AllNetcore_AllArchs.Where(n => n.ParsedVersion >= new Version(3, 0))).ToArray();
        internal static readonly TargetFramework[] Netfx_And_Netcore3Plus = Netfx_And_Netcore3Plus_AllArchs.Where(f => !f.ProcessKind.Is32Bit()).ToArray();
    }
}

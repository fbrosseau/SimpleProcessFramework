using Spfx.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Spfx.Utilities
{
    internal static class ProcessConfig
    {
        internal static string GetCodeBase(TargetFramework fw, ProcessClusterConfiguration config)
        {
            if (config.RuntimeCodeBases.TryGetValue(fw, out var dir))
                return dir;

            if (fw == TargetFramework.CurrentFramework || fw.ProcessKind.IsFakeProcess())
                return PathHelper.CurrentBinFolder.FullName;

            string relativeFolder;
            if (fw.ProcessKind.IsNetfx())
            {
                relativeFolder = "..\\net48";
            }
            else if (fw.ProcessKind.IsNetcore())
            {
                var runtime = NetcoreHelper.GetBestNetcoreRuntime((fw as NetcoreTargetFramework)?.TargetRuntime, fw.ProcessKind);
                relativeFolder = "../" + NetcoreHelper.GetDefaultNetcoreBinSubfolderName(runtime);
            }
            else
            {
                throw new ArgumentException("Framework not handled: " + fw);
            }

            return PathHelper.GetFullPath(relativeFolder);
        }

        public static readonly string DefaultNetcoreProcess = GetDefaultExecutableName(NetcoreTargetFramework.Default);
        public static readonly string DefaultNetcoreProcess32 = GetDefaultExecutableName(NetcoreTargetFramework.Default32);

        internal static string GetDefaultExecutableName(TargetFramework fw, ProcessClusterConfiguration config = null)
        {
            string name = null;
            if (config?.DefaultExecutableNames.TryGetValue(fw, out name) == true)
                return name;

            var suffix = fw.ProcessKind.Is32Bit() ? "32" : "";

            if (fw.ProcessKind.IsNetfx())
                return "Spfx.Process.Netfx" + suffix;
            return "Spfx.Process.Netcore" + suffix;
        }
    }
}

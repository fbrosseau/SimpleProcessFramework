using Spfx.Interfaces;
using Spfx.Utilities;
using Spfx.Utilities.Runtime;
using System;

namespace Spfx.Runtime.Server.Processes
{
    internal static class ProcessSpawnHelper
    {
        internal static string GetDefaultRuntimeCodeBase(ProcessKind processKind)
        {
            if (processKind == HostFeaturesHelper.LocalProcessKind)
                return PathHelper.CurrentBinFolder.FullName;

            if (HostFeaturesHelper.LocalProcessKind.IsNetfxProcess() && processKind.IsNetfxProcess())
                return PathHelper.CurrentBinFolder.FullName;

            string relativeCodebase;

            switch (processKind)
            {
                case ProcessKind.Netfx:
                case ProcessKind.Netfx32:
                    relativeCodebase = ProcessClusterConfiguration.DefaultDefaultNetfxCodeBase;
                    break;
                case ProcessKind.Netcore:
                case ProcessKind.Netcore32:
                case ProcessKind.Wsl:
                    var runtime = NetcoreInfo.GetBestNetcoreRuntime("2", processKind);
                    relativeCodebase = "../" + NetcoreInfo.GetDefaultNetcoreBinSubfolderName(runtime);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(processKind));
            }

            return PathHelper.GetFullPath(relativeCodebase);
        }
    }
}

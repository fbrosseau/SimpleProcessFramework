using Spfx.Interfaces;
using Spfx.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Spfx.Runtime.Server.Processes
{
    internal static class ProcessSpawnHelper
    {
        internal static string GetDefaultRuntimeCodeBase(ProcessKind processKind)
        {
            if (processKind == HostFeaturesHelper.LocalProcessKind)
                return PathHelper.CurrentBinFolder.FullName;

            if (HostFeaturesHelper.LocalProcessKind.IsNetfx() && processKind.IsNetfx())
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
                    relativeCodebase = "../netcoreapp" + NetcoreHelper.GetBestNetcoreRuntime("2");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(processKind));
            }

            return PathHelper.GetFullPath(relativeCodebase);
        }
    }
}

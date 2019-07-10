using System;
using System.Collections.Generic;
using Spfx.Interfaces;
using Spfx.Utilities;

namespace Spfx.Runtime.Server.Processes
{
    internal class WslProcessStartupParameters : NetcoreProcessStartupParameters
    {
        private static readonly string s_windowsVersionOfWslRoot = ProcessSpawnHelper.GetDefaultRuntimeCodeBase(ProcessKind.Wsl);
        private static readonly Lazy<string> s_wslRootBinPath = new Lazy<string>(() => WslUtilities.GetLinuxPath(s_windowsVersionOfWslRoot), false);

        protected override string GetWorkingDirectory()
            => s_wslRootBinPath.Value;
        protected override string DotNetPath
            => "dotnet";

        protected override void CreateFinalArguments(List<string> processArguments)
        {
            base.CreateFinalArguments(processArguments);
            processArguments.Insert(0, WslUtilities.WslExeFullPath);
        }

        protected override string GetFinalExecutableName(string executableName)
        {
            return WslUtilities.GetLinuxPath(executableName);
        }
    }
}

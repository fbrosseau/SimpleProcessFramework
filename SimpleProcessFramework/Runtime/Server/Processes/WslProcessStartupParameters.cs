using System;
using System.Collections.Generic;
using System.IO;
using Spfx.Interfaces;
using Spfx.Utilities;

namespace Spfx.Runtime.Server.Processes
{
    internal class WslProcessStartupParameters : NetcoreProcessStartupParameters
    {
        private static readonly string s_windowsVersionOfWslRoot = ProcessSpawnHelper.GetDefaultRuntimeCodeBase(ProcessKind.Wsl);

        protected override string GetWorkingDirectory()
            => s_windowsVersionOfWslRoot;
        protected override string DotNetPath
            => "dotnet";

        protected override void CreateFinalArguments(List<string> processArguments)
        {
            base.CreateFinalArguments(processArguments);
            processArguments.Insert(0, WslUtilities.WslExeFullPath);
        }

        protected override string GetUserExecutableFullPath(string executableName)
        {
            var dir = Path.GetDirectoryName(executableName);
            dir = WslUtilities.GetCachedLinuxPath(dir);
            if (!dir.EndsWith("/", StringComparison.OrdinalIgnoreCase))
                dir += "/";

            var file = Path.GetFileName(executableName);

            return dir + file;
        }
    }
}

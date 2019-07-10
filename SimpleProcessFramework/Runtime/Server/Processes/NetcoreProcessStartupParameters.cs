using System;
using System.Collections.Generic;
using Spfx.Interfaces;
using Spfx.Utilities;

namespace Spfx.Runtime.Server.Processes
{
    internal class NetcoreProcessStartupParameters : GenericProcessStartupParameters
    {
        protected override void CreateFinalArguments(List<string> processArguments)
        {
            base.CreateFinalArguments(processArguments);

            processArguments.Insert(0, DotNetPath);

            if (!string.IsNullOrEmpty(ProcessCreationInfo.SpecificRuntimeVersion))
            {
                var selectedVersion = NetcoreHelper.GetBestNetcoreRuntime(ProcessCreationInfo.SpecificRuntimeVersion, !ProcessKind.Is32Bit() || !Environment.Is64BitOperatingSystem);
                if (string.IsNullOrWhiteSpace(selectedVersion))
                    throw new InvalidOperationException("There is no installed runtime matching " + ProcessCreationInfo.SpecificRuntimeVersion);

                processArguments.Insert(1, "--fx-version");
                processArguments.Insert(2, selectedVersion);
            }
        }

        protected virtual string DotNetPath => NetcoreHelper.GetNetCoreHostPath(ProcessKind != ProcessKind.Netcore32);
    }
}

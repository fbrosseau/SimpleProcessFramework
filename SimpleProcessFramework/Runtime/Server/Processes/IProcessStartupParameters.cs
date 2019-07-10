using System.Collections.Generic;
using Spfx.Interfaces;

namespace Spfx.Runtime.Server.Processes
{
    internal interface IProcessStartupParameters
    {
        string WorkingDirectory { get; }
        string ExecutableName { get; }
        string CommandLineArguments { get; }
        IReadOnlyDictionary<string, string> EnvironmentBlock { get; }

        void Initialize(ProcessClusterConfiguration config, ProcessCreationInfo processCreationInfo);
    }
}

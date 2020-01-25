using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Runtime.Server.Processes;
using System.Diagnostics;
using Spfx.Subprocess;
using FluentAssertions;
using Spfx.Utilities;

namespace Spfx.Tests.Integration
{
    public class HostExecutableTests : CommonSpfxIntegrationTestsClass
    {
        [Test/*, Parallelizable(ParallelScope.Children)*/]
        [TestCaseSource(nameof(Netfx_And_AllNetcore_AllArchs))]
        public void HostExecutable_ValidateBadArgsExit(TargetFramework fw)
        {
            var builder = new CommandLineBuilder(DefaultTestResolver, ProcessClusterConfiguration.Default, new ProcessCreationInfo
            {
                TargetFramework = fw,
                ExtraCommandLineArguments = new[] { SubprocessMainShared.CommandLineArgs.CmdLinePrefix }
            });

            var processStartInfo = builder.CreateProcessStartupInfo();
            var proc = Process.Start(processStartInfo);
            proc.PrepareExitCode();

            proc.WaitForExit(5000).Should().BeTrue();
            proc.ExitCode.Should().Be((int)SubprocessMainShared.SubprocessExitCodes.BadCommandLine);
        }
    }
}
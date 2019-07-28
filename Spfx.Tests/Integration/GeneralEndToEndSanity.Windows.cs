#if !UNIX_TESTS_BUILD_ONLY

using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Utilities;
using static Spfx.Tests.TestUtilities;

namespace Spfx.Tests.Integration
{
    public partial class GeneralEndToEndSanity
    {
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        [Category("Netfx-Only"), Category("Windows-Only")]
        public void AppDomainCallbackToOtherProcess()
        {
            if (!HostFeaturesHelper.IsWindows || !HostFeaturesHelper.LocalProcessKind.IsNetfx())
                Assert.Ignore("AppDomains not supported");
            TestCallback(ProcessKind.AppDomain, callbackInMaster: false);
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        [Category("Netfx-Only"), Category("Windows-Only")]
        public void BasicDefaultNameSubprocess_Netfx() => CreateAndDestroySuccessfulSubprocess(p => p.TargetFramework = TargetFramework.Create(ProcessKind.Netfx));
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        [Category("Netfx-Only"), Category("Windows-Only")]
        public void BasicDefaultNameSubprocess_Netfx32() => CreateAndDestroySuccessfulSubprocess(p => p.TargetFramework = TargetFramework.Create(ProcessKind.Netfx32));
        
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        [Category("Windows-Only")]
        public void BasicDefaultNameSubprocess_Wsl()
        {
            if (!HostFeaturesHelper.IsWslSupported)
                Assert.Ignore("WSL not supported");

            CreateAndDestroySuccessfulSubprocess(p => p.TargetFramework = NetcoreTargetFramework.Create(ProcessKind.Wsl, "2"));
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        [Category("Netfx-Only"), Category("Windows-Only")]
        public void BasicCustomNameSubprocess()
        {
            const string customProcessName = "Spfx.UnitTests.agj90gj09jg0a94jg094jg";

            DeleteFileIfExists(customProcessName + ".exe");
            DeleteFileIfExists(customProcessName + ".dll");

            CreateAndDestroySuccessfulSubprocess(procInfo =>
            {
                procInfo.TargetFramework = TargetFramework.Create(ProcessKind.Netfx);
                procInfo.ProcessName = customProcessName;
            });
        }
    }
}

#endif

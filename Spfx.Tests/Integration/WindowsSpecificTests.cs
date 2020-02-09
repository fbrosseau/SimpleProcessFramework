#if WINDOWS_BUILD

using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Utilities.Runtime;

namespace Spfx.Tests.Integration
{
    [TestFixture(SanityTestOptions.Tcp)]
    [TestFixture(SanityTestOptions.Interprocess)]
    //NOT [Parallelizable]
    internal class WindowsSpecificTests : CommonSpfxIntegrationTestsClass
    {
        public WindowsSpecificTests(SanityTestOptions options)
            : base(options)
        {
        }

        [Test]
        public void BasicDefaultSubprocess_DefaultKind_Managed()
        {
            if (!WslUtilities.IsWslSupported)
                Assert.Pass("WSL not supported"); // Assert.Ignore reports as error in the pipeline...
            CreateAndDestroySuccessfulSubprocess(
                p => p.TargetFramework = TargetFramework.Create(DefaultProcessKind),
                customConfig: cfg => cfg.UseGenericProcessSpawnOnWindows = true);
        }

        [Test]
        public void BasicDefaultSubprocess_Wsl_Managed()
        {
            if (!WslUtilities.IsWslSupported)
                Assert.Pass("WSL not supported"); // Assert.Ignore reports as error in the pipeline...
            CreateAndDestroySuccessfulSubprocess(
                p => p.TargetFramework = TargetFramework.Create(ProcessKind.Wsl),
                customConfig: cfg => cfg.UseGenericProcessSpawnOnWindows = true);
        }

#if NETFRAMEWORK
        [Test]
        [Category("NetfxHost-Only"), Category("Netfx-Only"), Category("Windows-Only")]
        public void BasicDefaultSubprocess_AppDomain()
        {
            CreateAndDestroySuccessfulSubprocess(
                 p => p.TargetFramework = TargetFramework.Create(ProcessKind.AppDomain),
                 customConfig: cfg => cfg.UseGenericProcessSpawnOnWindows = true);
        }
#endif // NETFRAMEWORK
    }
}

#endif // WINDOWS_BUILD

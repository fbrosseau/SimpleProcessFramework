#if !UNIX_TESTS_BUILD_ONLY

using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Runtime.Exceptions;
using Spfx.Utilities;
using System;
using System.Collections.Generic;
using static Spfx.Tests.TestUtilities;

namespace Spfx.Tests.Integration
{
    public partial class GeneralEndToEndSanity
    {
#if NETFRAMEWORK
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        [Category("NetfxHost-Only"), Category("Netfx-Only"), Category("Windows-Only")]
        public void AppDomainCallbackToOtherProcess()
        {
            if (!HostFeaturesHelper.IsWindows || !HostFeaturesHelper.LocalProcessKind.IsNetfx())
                Assert.Ignore("AppDomains not supported");
            TestCallback(ProcessKind.AppDomain, callbackInMaster: false);
        }
#endif

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        [Category("Windows-Only")]
        [TestCaseSource(nameof(Netfx_AllArchs))]
        public void CustomNameSubprocess_NewFileAllowed(TargetFramework targetFramework)
        {
            string customProcessName = "Spfx.TestCustomName." + Guid.NewGuid().GetHashCode().ToString("X8");

            void CleanupFile()
            {
                DeleteFileIfExists(customProcessName + ".exe");
                DeleteFileIfExists(customProcessName + ".dll");
            }

            CleanupFile();
            try
            {
                CustomNameSubprocessTest(targetFramework, customProcessName, allowCreate: true);
            }
            finally
            {
                CleanupFile();
            }
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        [Category("Windows-Only")]
        [TestCaseSource(nameof(Netfx_And_Netcore3Plus_AllArchs))]
        public void CustomNameSubprocessTestDenied(TargetFramework targetFramework)
        {
            string customProcessName = "Spfx.UnitTests." + Guid.NewGuid().ToString("N");
            AssertThrows(() =>
            {
                CustomNameSubprocessTest(targetFramework, customProcessName);
            }, (MissingSubprocessExecutableException ex) =>
            {
                Assert.AreEqual(customProcessName, ex.Filename);
            });
        }

        private void CustomNameSubprocessTest(TargetFramework targetFramework, string customProcessName, bool allowCreate = false)
        {
            using var cluster = CreateTestCluster(cfg =>
            {
                cfg.CreateExecutablesIfMissing = allowCreate;
            });

            using var subprocess = CreateSuccessfulSubprocess(cluster, procInfo =>
            {
                procInfo.TargetFramework = targetFramework;
                procInfo.ProcessName = customProcessName;
            });
        }
    }
}

#endif

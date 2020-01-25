using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Runtime.Exceptions;
using Spfx.Utilities;
using System;
using System.Collections.Generic;
using System.IO;

namespace Spfx.Tests.Integration
{
    public class CustomHostsTests : CommonSpfxIntegrationTestsClass
    {
        private static IEnumerable<object[]> GetAllValidPreExistingFileCombinations()
        {
            foreach (var fw in Netfx_AllArchs)
                yield return new object[] { fw, SharedTestcustomHostUtilities.ExecutableName };

            foreach (var fw in AllNetcore_AllArchs)
            {
                yield return new object[] { fw, SharedTestcustomHostUtilities.ExecutableName };
                yield return new object[] { fw, SharedTestcustomHostUtilities.StandaloneDllName };
                yield return new object[] { fw, $"{SharedTestcustomHostUtilities.ExecutableName}.dll" };
                yield return new object[] { fw, $"{SharedTestcustomHostUtilities.StandaloneDllName}.dll" };
            }

            foreach (var fw in Netfx_And_Netcore3Plus_AllArchs)
                yield return new object[] { fw, $"{SharedTestcustomHostUtilities.ExecutableName}.exe" };
        }

        [Test, Parallelizable(ParallelScope.Children)]
        [TestCaseSource(nameof(GetAllValidPreExistingFileCombinations))]
        public void CustomNameSubprocess_ValidPreExistingFile(TargetFramework targetFramework, string customProcessName)
        {
            CustomNameSubprocessTest(targetFramework, customProcessName, validateCustomEntryPoint: true);
        }

        [Test, Parallelizable(ParallelScope.Children)]
        [TestCaseSource(nameof(Netfx_And_Netcore3Plus_AllArchs))]
        public void CustomNameSubprocess_NewFileAllowed(TargetFramework targetFramework)
        {
            string customProcessName = GetNewCustomHostName();

            void CleanupFile()
            {
                DeleteFileIfExists(customProcessName + ".exe");
                DeleteFileIfExists(customProcessName + ".dll");
            }

            CleanupFile();
            try
            {
                CustomNameSubprocessTest(targetFramework, customProcessName, allowCreate: true, validateCustomEntryPoint: false);
            }
            finally
            {
                CleanupFile();
            }
        }

        [Test, Parallelizable(ParallelScope.Children)]
        [TestCaseSource(nameof(Netfx_And_Netcore3Plus_AllArchs))]
        public void CustomNameSubprocess_CustomHostDenied(TargetFramework targetFramework)
        {
            string customProcessName = GetNewCustomHostName();
            AssertThrows(() =>
            {
                CustomNameSubprocessTest(targetFramework, customProcessName);
            }, (MissingSubprocessExecutableException ex) =>
            {
                Assert.AreEqual(customProcessName, ex.Filename);
            });
        }

        private const string CustomHostNamePrefix = "Spfx.Tests.CustomName.";

        private string GetNewCustomHostName()
        {
            return CustomHostNamePrefix + Guid.NewGuid().GetHashCode().ToString("X8") + "_";
        }

        [OneTimeTearDown]
        public void ClassTearDown()
        {
            try
            {
                // best effort to delete hosts...
                foreach (var f in PathHelper.CurrentBinFolder.GetFiles(CustomHostNamePrefix + "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        f.Delete();
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void CustomNameSubprocessTest(TargetFramework targetFramework, string customProcessName, bool validateCustomEntryPoint = false, bool allowCreate = false)
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

            if (validateCustomEntryPoint)
                Unwrap(subprocess.TestInterface.ValidateCustomProcessEntryPoint());
        }
    }
}
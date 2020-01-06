using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Server.Processes;
using Spfx.Serialization;
using Spfx.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using static Spfx.Tests.TestUtilities;

namespace Spfx.Tests.Integration
{
    [TestFixture(SanityTestOptions.UseTcpProxy)]
    [TestFixture(SanityTestOptions.UseIpcProxy)]
    public partial class GeneralEndToEndSanity : CommonTestClass
    {
        private long s_nextUniqueId;

        public GeneralEndToEndSanity(SanityTestOptions options)
            : base(options)
        {
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        [TestCaseSource(nameof(AllGenericSupportedFrameworks))]
        public void BasicDefaultSubprocess(TargetFramework fw)
            => CreateAndDestroySuccessfulSubprocess(p => p.TargetFramework = fw);

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicTestInMasterProcess()
        {
            using var cluster = CreateTestCluster();
            CreateAndValidateTestInterface(cluster, cluster.MasterProcess.UniqueAddress);
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicProcessCallbackToMaster() => TestCallback(DefaultProcessKind);
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void FakeProcessCallbackToMaster() => TestCallback(ProcessKind.DirectlyInRootProcess);
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void FakeProcessCallbackToOtherProcess() => TestCallback(ProcessKind.DirectlyInRootProcess, callbackInMaster: false);
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicProcessCallbackToOtherProcess() => TestCallback(DefaultProcessKind, callbackInMaster: false);

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        [TestCaseSource(nameof(AllNetcore_AllArchs))]
        public void BasicNetcore_SpecificRuntime(TargetFramework fw) => CreateAndDestroySuccessfulSubprocess(p => { p.TargetFramework = fw; });

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        [TestCaseSource(nameof(Simple_Netfx_And_Netcore))]
        public void BasicEnvironmentVariableSubprocess(TargetFramework fw)
        {
            var envVar = "CustomEnvVar_" + Guid.NewGuid().ToString("N");
            var envValue = "CustomEnvVar_" + Guid.NewGuid().ToString("N");

            using var cluster = CreateTestCluster();
            using var iface = CreateSuccessfulSubprocess(cluster, procInfo =>
            {
                procInfo.TargetFramework = fw;
                procInfo.ExtraEnvironmentVariables = new[] { new StringKeyValuePair(envVar, envValue) };
            });
             
            Assert.AreEqual(envValue, Unwrap(iface.TestInterface.GetEnvironmentVariable(envVar)));
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void ThrowCustomException_NotSerializable()
        {
            using var cluster = CreateTestCluster();
            using var svc = CreateSuccessfulSubprocess(cluster, p => p.TargetFramework = TargetFramework.DirectlyInRootProcess);

            var text = Guid.NewGuid().ToString("N");
            ExpectException(svc.TestInterface.GetDummyValue(typeof(CustomException_NotMarshalled), exceptionText: text), typeof(RemoteException), expectedText: text, expectedStackFrame: TestInterface.ThrowingMethodName);
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void ThrowCustomException_Serializable()
        {
            using var cluster = CreateTestCluster();
            using var svc = CreateSuccessfulSubprocess(cluster, p => p.TargetFramework = TargetFramework.DirectlyInRootProcess);

            var text = Guid.NewGuid().ToString("N");
            ExpectException<CustomException_Marshalled>(svc.TestInterface.GetDummyValue(typeof(CustomException_Marshalled), exceptionText: text),
                ex =>
                {
                    ex.AssertValues(text);
                });
        }

        private static IEnumerable<object[]> GetAllValidPreExistingFileCombinations()
        {
            foreach (var fw in Netfx_AllArchs)
                yield return new object[] { fw, TestCustomHostExe.ExecutableName };

            foreach (var fw in AllNetcore_AllArchs)
            {
                yield return new object[] { fw, TestCustomHostExe.ExecutableName };
                yield return new object[] { fw, TestCustomHostExe.StandaloneDllName };
                yield return new object[] { fw, $"{TestCustomHostExe.ExecutableName}.dll" };
                yield return new object[] { fw, $"{TestCustomHostExe.StandaloneDllName}.dll" };
            }

            foreach (var fw in Netfx_And_Netcore3Plus_AllArchs)
                yield return new object[] { fw, $"{TestCustomHostExe.ExecutableName}.exe" };
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable(ParallelScope.Children)*/]
        [TestCaseSource(nameof(GetAllValidPreExistingFileCombinations))]
        public void CustomNameSubprocess_ValidPreExistingFile(TargetFramework targetFramework, string customProcessName)
        {
            CustomNameSubprocessTest(targetFramework, customProcessName, validateCustomEntryPoint: true);
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
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
                CustomNameSubprocessTest(targetFramework, customProcessName, allowCreate: true);
            }
            finally
            {
                CleanupFile();
            }
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        [TestCaseSource(nameof(Netfx_And_Netcore3Plus_AllArchs))]
        public void CustomNameSubprocessTestDenied(TargetFramework targetFramework)
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

        [OneTimeSetUp]
        public void ClassSetUp()
        {
            NetcoreHelper.GetInstalledNetcoreRuntimes();
            if (HostFeaturesHelper.Is32BitSupported)
                NetcoreHelper.GetInstalledNetcoreRuntimes(false);
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

        private void TestCallback(ProcessKind processKind, bool callbackInMaster = true)
        {
            using var cluster = CreateTestCluster();
            TestCallback(cluster, processKind, callbackInMaster);
        }

        private void TestCallback(ProcessCluster cluster, ProcessKind processKind, bool callbackInMaster = true)
        {
            var testServices = new List<TestInterfaceWrapper>();
            var test = CreateSuccessfulSubprocess(cluster, proc => proc.TargetFramework = TargetFramework.Create(processKind));

            testServices.Add(test);

            string targetProcess = WellKnownEndpoints.MasterProcessUniqueId;
            if (!callbackInMaster)
            {
                var test2 = CreateSuccessfulSubprocess(cluster, proc => proc.TargetFramework = TargetFramework.Create(processKind));
                testServices.Add(test2);
                targetProcess = ProcessProxy.GetEndpointAddress(test2.TestInterface).TargetProcess;
            }

            var proxy = CreateProxy(cluster);
            var targetEndpointBroker = CreateProxyInterface<IEndpointBroker>(proxy, cluster, targetProcess, WellKnownEndpoints.EndpointBroker);

            var callbackEndpoint = "Cb_" + GetNextUniqueId();

            Unwrap(targetEndpointBroker.CreateEndpoint(new EndpointCreationRequest
            {
                EndpointId = callbackEndpoint,
                EndpointType = typeof(ICallbackInterface),
                ImplementationType = typeof(CallbackInterface),
                Options = ProcessCreationOptions.ThrowIfExists
            }));

            var input = 5;
            var res = Unwrap(test.TestInterface.Callback($"/{targetProcess}/{callbackEndpoint}", input));
            Assert.AreEqual(input * 2, res);

            foreach (var svc in testServices)
            {
                svc.Dispose();
            }
        }

        private void CreateAndDestroySuccessfulSubprocess(Action<ProcessCreationInfo> requestCustomization = null, Action<ProcessClusterConfiguration> customConfig = null)
        {
            using var cluster = CreateTestCluster(customConfig);
            CreateSuccessfulSubprocess(cluster, requestCustomization).Dispose();
        }

        private static void DisposeTestProcess(TestInterfaceWrapper testInterface)
        {
            var svc = testInterface.TestInterface;
            var proc = testInterface.ProcessInfo;
            var pid = proc.Id;

            var processName = ProcessProxy.GetEndpointAddress(svc).TargetProcess;

            Log("Invoking DestroyProcess for " + processName);
            Unwrap(testInterface.Cluster.ProcessBroker.DestroyProcess(processName));

            if (pid != Process.GetCurrentProcess().Id)
            {
                Log("Waiting for real exit of process" + pid);
                proc.WaitForExit(DefaultTestTimeout);
            }
        }

        public class TestInterfaceWrapper : IDisposable
        {
            public ITestInterface TestInterface { get; }
            public Process ProcessInfo { get; }
            public ProcessCluster Cluster { get; }
            public string PhysicalProcessName { get; }
            public bool CanWaitForExit { get; }

            public TestInterfaceWrapper(ProcessCluster cluster, Process processObject, ITestInterface iface)
            {
                TestInterface = iface;
                Cluster = cluster;
                ProcessInfo = processObject;
                PhysicalProcessName = processObject.ProcessName;

                try
                {
                    CanWaitForExit = !"system".Equals(processObject.ProcessName, StringComparison.OrdinalIgnoreCase);

                    if (CanWaitForExit)
                    {
                        // this is necessary for later calls to WaitForExit to succeed...
                        _ = ProcessInfo.SafeHandle;
                    }
                }
                catch
                {
                    CanWaitForExit = false;
                }
            }

            public void Dispose()
            {
                DisposeTestProcess(this);
            }
        }

        private TestInterfaceWrapper CreateSuccessfulSubprocess(ProcessCluster cluster, Action<ProcessCreationInfo> requestCustomization = null)
        {
            var procId = "Proc_" + GetNextUniqueId();
            var req = new ProcessCreationRequest
            {
                Options = ProcessCreationOptions.ThrowIfExists,
                ProcessInfo = new ProcessCreationInfo
                {
                    ProcessUniqueId = procId,
                    TargetFramework = TargetFramework.Create(DefaultProcessKind),
                    ManuallyRedirectConsole = IsInMsTest
                }
            };

            requestCustomization?.Invoke(req.ProcessInfo);

            var expectedFramework = req.ProcessInfo.TargetFramework;
            var expectedProcessKind = expectedFramework.ProcessKind;
            var requestedRuntime = req.ProcessInfo.TargetFramework;
            string targetNetcoreRuntime = null;
            if (requestedRuntime is NetcoreTargetFramework netcore && !string.IsNullOrWhiteSpace(netcore.TargetRuntime))
            {
                targetNetcoreRuntime = netcore.TargetRuntime;
                if (NetcoreHelper.GetBestNetcoreRuntime(netcore.TargetRuntime) == null)
                    Assert.Fail($".net core runtime {requestedRuntime} is not supported by this host. The supported runtimes are: \r\n"
                        + string.Join("\r\n", NetcoreHelper.GetInstalledNetcoreRuntimes().Select(r => "- " + r)));
            }

            if (!requestedRuntime.IsSupportedByCurrentProcess(cluster.Configuration, out var details))
                Assert.Fail(details);
            
            var expectedProcessName = req.ProcessInfo.ProcessName;
            if (string.IsNullOrWhiteSpace(expectedProcessName))
            {
                if (!expectedProcessKind.IsFakeProcess()
                    && !expectedProcessKind.IsNetcore())
                {
                    expectedProcessName = ProcessConfig.GetDefaultExecutableName(expectedFramework, cluster.Configuration);
                }
            }

            if (!string.IsNullOrWhiteSpace(expectedProcessName))
            {
                if (expectedProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    expectedProcessName = expectedProcessName.Substring(0, expectedProcessName.Length - 4);

                if (!string.IsNullOrWhiteSpace(expectedProcessName) && expectedProcessKind.Is32Bit() && cluster.Configuration.Append32BitSuffix
                    && !expectedProcessName.EndsWith(cluster.Configuration.SuffixFor32BitProcesses, StringComparison.OrdinalIgnoreCase)
                    && !expectedProcessName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    expectedProcessName += cluster.Configuration.SuffixFor32BitProcesses;
                }
            }

            int expectedPtrSize;

            OsKind expectedOsKind;

            if (expectedProcessKind == ProcessKind.Wsl)
            {
                expectedOsKind = OsKind.Linux;
            }
            else
            {
                expectedOsKind = HostFeaturesHelper.LocalMachineOsKind;
            }
           
            Log("CreateProcess...");
            var createdNew = Unwrap(cluster.ProcessBroker.CreateProcess(req));
            Assert.AreEqual(ProcessCreationOutcome.CreatedNew, createdNew);

            var processInfo = Unwrap(cluster.ProcessBroker.GetProcessInformation(procId));
            Assert.AreEqual(procId, processInfo.ProcessName);
            Assert.AreEqual(expectedProcessKind, processInfo.Framework.ProcessKind);
            Assert.AreNotEqual(0, processInfo.OsPid);

            var iface = CreateAndValidateTestInterface(cluster, ProcessEndpointAddress.Parse($"/{procId}/"));

            Log("Doing basic validation on target process..");

            bool expectDotNetExe = expectedProcessName?.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) == true;

            if(expectedProcessKind.IsNetcore())
            {
                var ver = Unwrap(iface.GetNetCoreVersion());
                expectDotNetExe |= ver.Major < 3 || expectedProcessName?.StartsWith(TestCustomHostExe.StandaloneDllName) == true;

                if (!string.IsNullOrWhiteSpace(targetNetcoreRuntime))
                {
                    var requestedVersion = NetcoreHelper.ParseNetcoreVersion(targetNetcoreRuntime);
                    if (requestedVersion.Major >= 3) // it's not possible to accurately tell netcore version before 3. Assume it just works for < 2.
                    {
                        Assert.GreaterOrEqual(ver, requestedVersion, "Unexpected runtime version");
                    }
                }
            }

            var realProcName = Unwrap(iface.GetActualProcessName());
            if (realProcName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                realProcName = realProcName.Substring(0, realProcName.Length - 4);

            if (expectDotNetExe)
            {
                if(!realProcName.EndsWith("dotnet", StringComparison.OrdinalIgnoreCase))
                    Assert.Fail("Expected to be hosted by dotnet.exe, actual was " + realProcName);
            }
            else if (expectedProcessName != null)
            {
                Assert.AreEqual(expectedProcessName, realProcName);
            }

            var processObject = Process.GetProcessById(processInfo.OsPid);

            if (expectedProcessKind.IsFakeProcess())
            {
                expectedPtrSize = IntPtr.Size;
                expectedProcessKind = HostFeaturesHelper.LocalProcessKind;
            }
            else
            {
                expectedPtrSize = expectedProcessKind.Is32Bit() ? 4 : 8;
            }

            if (expectedProcessKind == ProcessKind.Wsl)
                expectedProcessKind = ProcessKind.Netcore;

            Assert.AreEqual(expectedProcessKind, Unwrap(iface.GetRealProcessKind()));
            Assert.AreEqual(expectedOsKind, Unwrap(iface.GetOsKind()));
            Assert.AreEqual(expectedPtrSize, Unwrap(iface.GetPointerSize()));

            using (var cts = new CancellationTokenSource())
            {
                var cancellableCall = iface.GetDummyValue(delay: Timeout.InfiniteTimeSpan, ct: cts.Token);
                Assert.IsFalse(cancellableCall.IsCompleted);
                cts.Cancel();
                ExpectException(cancellableCall);
            }

            Log("CreateSuccessfulSubprocess success");

            return new TestInterfaceWrapper(cluster, processObject, iface);
        }

        private ITestInterface CreateAndValidateTestInterface(ProcessCluster cluster, ProcessEndpointAddress processEndpointAddress)
        {
            var testEndpoint = "EP_" + GetNextUniqueId();

            var proxy = CreateProxy(cluster);

            var endpointBroker = CreateProxyInterface<IEndpointBroker>(proxy, cluster, processEndpointAddress.TargetProcess, WellKnownEndpoints.EndpointBroker);
            Unwrap(endpointBroker.CreateEndpoint(testEndpoint, typeof(ITestInterface), typeof(TestInterface)));
            Log("Create Endpoint " + testEndpoint);

            var testInterface = CreateProxyInterface<ITestInterface>(proxy, cluster, processEndpointAddress.TargetProcess, testEndpoint);
            var res = Unwrap(testInterface.GetDummyValue());
            TestReturnValue.Verify(res);
            Log("Validated Endpoint " + testEndpoint);

            return testInterface;
        }

        private string GetNextUniqueId()
        {
            return Interlocked.Increment(ref s_nextUniqueId).ToString("X4");
        }
    }
}
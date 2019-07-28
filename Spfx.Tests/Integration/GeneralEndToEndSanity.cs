using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Runtime.Server.Processes;
using Spfx.Serialization;
using Spfx.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static Spfx.Tests.TestUtilities;

namespace Spfx.Tests.Integration
{
    [TestFixture(SanityTestOptions.UseTcpProxy)]
    [TestFixture(SanityTestOptions.UseIpcProxy)]
    public partial class GeneralEndToEndSanity : CommonTestClass
    {
        public GeneralEndToEndSanity(SanityTestOptions options)
            : base(options)
        {
        }
        
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicDefaultNameSubprocess() => CreateAndDestroySuccessfulSubprocess();
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicDefaultNameSubprocess_NetCore() => CreateAndDestroySuccessfulSubprocess(p => p.TargetFramework = TargetFramework.Create(ProcessKind.Netcore));
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicDefaultNameSubprocess_NetCore32() => CreateAndDestroySuccessfulSubprocess(p => p.TargetFramework = TargetFramework.Create(ProcessKind.Netcore32));

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicTestInMasterProcess()
        {
            using (var cluster = CreateTestCluster())
            {
                CreateAndValidateTestInterface(cluster, cluster.MasterProcess.UniqueAddress);
            }
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
        public void BasicNetcore_Runtime2X() => CreateAndDestroySuccessfulSubprocess(p => { p.TargetFramework = NetcoreTargetFramework.Create("2"); });
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicNetcore_Runtime3X() => CreateAndDestroySuccessfulSubprocess(p => { p.TargetFramework = NetcoreTargetFramework.Create("3"); });

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicEnvironmentVariableSubprocess()
        {
            var envVar = "AWGJIEAJWIGJIAWE";
            var envValue = "JIAEWIGJEWIGHJRIEHJI";

            using (var cluster = CreateTestCluster())
            {
                var iface = CreateSuccessfulSubprocess(cluster, procInfo =>
                {
                    procInfo.ExtraEnvironmentVariables = new[] { new ProcessCreationInfo.KeyValuePair(envVar, envValue) };
                });

                using (iface)
                {
                    Assert.AreEqual(envValue, Unwrap(iface.TestInterface.GetEnvironmentVariable(envVar)));
                }
            }
        }

        public class CustomExceptionNotMarshalled : Exception
        {
            public CustomExceptionNotMarshalled(string msg)
                : base(msg)
            {
            }
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void ThrowCustomException()
        {
            using (var cluster = CreateTestCluster())
            using (var svc = CreateSuccessfulSubprocess(cluster, p => p.TargetFramework = TargetFramework.DirectlyInRootProcess))
            {
                var text = Guid.NewGuid().ToString("N");
                ExpectException(svc.TestInterface.GetDummyValue(typeof(CustomExceptionNotMarshalled), exceptionText: text), typeof(RemoteException), expectedText: text, expectedStackFrame: TestInterface.ThrowingMethodName);
            }
        }

        private void TestCallback(ProcessKind processKind, bool callbackInMaster = true)
        {
            using (var cluster = CreateTestCluster())
            {
                TestCallback(cluster, processKind, callbackInMaster);
            }
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

            var callbackEndpoint = Guid.NewGuid().ToString("N");

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

        private void CreateAndDestroySuccessfulSubprocess(Action<ProcessCreationInfo> requestCustomization = null)
        {
            using (var cluster = CreateTestCluster())
            {
                CreateSuccessfulSubprocess(cluster, requestCustomization).Dispose();
            }
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
            var procId = Guid.NewGuid().ToString("N");
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
                    Assert.Fail(".net core runtime " + requestedRuntime + " is not supported by this host");
            }

            if (!requestedRuntime.IsSupportedByCurrentProcess(cluster.Configuration, out var details))
                Assert.Fail(details);
            
            var expectedProcessName = req.ProcessInfo.ProcessName;
            if (string.IsNullOrWhiteSpace(expectedProcessName)
                && !expectedProcessKind.IsFakeProcess()
                && !expectedProcessKind.IsNetcore())
                expectedProcessName = GenericProcessStartupParameters.GetDefaultExecutableFileName(expectedProcessKind, ProcessClusterConfiguration.Default);

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

            if (expectedProcessName != null)
                Assert.AreEqual(expectedProcessName, Unwrap(iface.GetActualProcessName()));

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

            Log("GetRealProcessKind");
            Assert.AreEqual(expectedProcessKind, Unwrap(iface.GetRealProcessKind()));
            Log("GetOsKind");
            Assert.AreEqual(expectedOsKind, Unwrap(iface.GetOsKind()));
            Log("GetPointerSize");
            Assert.AreEqual(expectedPtrSize, Unwrap(iface.GetPointerSize()));

            if (!string.IsNullOrWhiteSpace(targetNetcoreRuntime))
            {
                var ver = Unwrap(iface.GetNetCoreVersion());
                var requestedVersion = NetcoreHelper.ParseNetcoreVersion(targetNetcoreRuntime);
                Assert.AreEqual(requestedVersion.Major, ver.Major, "Unexpected runtime version");

                if (requestedVersion.Minor > 0)
                    Assert.AreEqual(requestedVersion.Minor, ver.Minor, "Unexpected runtime version");
            }

            Log("CreateSuccessfulSubprocess success");

            return new TestInterfaceWrapper(cluster, processObject, iface);
        }

        private ITestInterface CreateAndValidateTestInterface(ProcessCluster cluster, ProcessEndpointAddress processEndpointAddress)
        {
            var testEndpoint = Guid.NewGuid().ToString("N");

            var proxy = CreateProxy(cluster);

            var endpointBroker = CreateProxyInterface<IEndpointBroker>(proxy, cluster, processEndpointAddress.TargetProcess, WellKnownEndpoints.EndpointBroker);
            endpointBroker.CreateEndpoint(testEndpoint, typeof(ITestInterface), typeof(TestInterface));
            Log("Create Endpoint " + testEndpoint);

            var testInterface = CreateProxyInterface<ITestInterface>(proxy, cluster, processEndpointAddress.TargetProcess, testEndpoint);
            var res = Unwrap(testInterface.GetDummyValue());
            DummyReturn.Verify(res);
            Log("Validated Endpoint " + testEndpoint);

            return testInterface;
        }
    }
}
using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Server.Processes;
using Spfx.Serialization;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static Spfx.Tests.TestUtilities;

namespace Spfx.Tests.Integration
{
    [TestFixture]
    public class GeneralEndToEndSanity : CommonTestClass
    {
        public static bool IsInMsTest { get; set; } = true;

        private static readonly ProcessKind DefaultProcessKind = ProcessClusterConfiguration.DefaultDefaultProcessKind;

#if NETFRAMEWORK
        private static readonly ProcessKind SimpleIsolationKind = ProcessKind.AppDomain;
#else
        private static readonly ProcessKind SimpleIsolationKind = ProcessKind.Netcore;
#endif

        private ProcessCluster CreateTestCluster()
        {
            return new ProcessCluster(new ProcessClusterConfiguration
            {
                EnableFakeProcesses = true
            });
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicDefaultNameSubprocess() => CreateAndDestroySuccessfulSubprocess();
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicDefaultNameSubprocess_Netfx() => CreateAndDestroySuccessfulSubprocess(p => p.ProcessKind = ProcessKind.Netfx);
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicDefaultNameSubprocess_Netfx32() => CreateAndDestroySuccessfulSubprocess(p => p.ProcessKind = ProcessKind.Netfx32);
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicDefaultNameSubprocess_NetCore() => CreateAndDestroySuccessfulSubprocess(p => p.ProcessKind = ProcessKind.Netcore);
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicDefaultNameSubprocess_NetCore32() => CreateAndDestroySuccessfulSubprocess(p => p.ProcessKind = ProcessKind.Netcore32);

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        [Category("Windows-Only")]
        public void BasicDefaultNameSubprocess_Wsl()
        {
            if (!HostFeaturesHelper.IsWslSupported)
                Assert.Ignore("WSL not supported");

            CreateAndDestroySuccessfulSubprocess(p => p.ProcessKind = ProcessKind.Wsl);
        }

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

#if NETFRAMEWORK
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        [Category("Windows-Only")]
        public void AppDomainCallbackToOtherProcess()
        {
            if (!HostFeaturesHelper.IsWindows || !HostFeaturesHelper.LocalProcessKind.IsNetfx())
                Assert.Ignore("AppDomains not supported");
            TestCallback(ProcessKind.AppDomain, callbackInMaster: false);
        }
#endif

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void FakeProcessCallbackToOtherProcess() => TestCallback(ProcessKind.DirectlyInRootProcess, callbackInMaster: false);
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicProcessCallbackToOtherProcess() => TestCallback(DefaultProcessKind, callbackInMaster: false);
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicNetcore_Runtime21() => CreateAndDestroySuccessfulSubprocess(p => { p.ProcessKind = ProcessKind.Netcore; p.SpecificRuntimeVersion = "2.1"; });
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicNetcore_Runtime30() => CreateAndDestroySuccessfulSubprocess(p => { p.ProcessKind = ProcessKind.Netcore; p.SpecificRuntimeVersion = "3.0"; });

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicEnvironmentVariableSubprocess()
        {
            var envVar = "AWGJIEAJWIGJIAWE";
            var envValue = "JIAEWIGJEWIGHJRIEHJI";

            using (var cluster = CreateTestCluster())
            {
                var iface = CreateSuccessfulSubprocess(cluster, procInfo =>
                {
                    procInfo.ExtraEnvironmentVariables = new[] { new ProcessCreationInfo.KeyValuePair(envVar, envValue) }.ToList();
                });

                using (iface)
                {
                    Assert.AreEqual(envValue, Unwrap(iface.TestInterface.GetEnvironmentVariable(envVar)));
                }
            }
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void BasicCustomNameSubprocess()
        {
            const string customProcessName = "Spfx.UnitTests.agj90gj09jg0a94jg094jg";

            DeleteFileIfExists(customProcessName + ".exe");
            DeleteFileIfExists(customProcessName + ".dll");

            CreateAndDestroySuccessfulSubprocess(procInfo =>
            {
                procInfo.ProcessKind = ProcessKind.Netfx;
                procInfo.ProcessName = customProcessName;
            });
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
            using (var svc = CreateSuccessfulSubprocess(cluster, p => p.ProcessKind = ProcessKind.DirectlyInRootProcess))
            {
                var text = Guid.NewGuid().ToString("N");
                UnwrapException(svc.TestInterface.GetDummyValue(typeof(CustomExceptionNotMarshalled), exceptionText: text), typeof(RemoteException), expectedText: text, expectedStackFrame: TestInterface.ThrowingMethodName);
            }
        }

        private class LongInitEndpoint : TestInterface
        {
            protected override async Task InitializeAsync()
            {
                await Task.Delay(1000);
                await base.InitializeAsync();
            }
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void ConcurrentCreateProcess_Throw() => ConcurrentCreateProcess(ProcessCreationOptions.ThrowIfExists);
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void ConcurrentCreateProcess_NoThrow() => ConcurrentCreateProcess(ProcessCreationOptions.ContinueIfExists);

        private void ConcurrentCreateProcess(ProcessCreationOptions mustCreateNewProcess)
        {
            using (var cluster = CreateTestCluster())
            {
                var tasks = new List<Task<Task<ProcessCreationOutcome>>>();
                var processId = "asdfasdf";

                const int concurrencyCount = 10;

                for (int i = 0; i < concurrencyCount; ++i)
                {
                    int innerI = i;
                    var t = Task.Run(() =>
                    {
                        return cluster.ProcessBroker.CreateProcess(new ProcessCreationRequest
                        {
                            Options = mustCreateNewProcess,
                            ProcessInfo = new ProcessCreationInfo
                            {
                                ProcessUniqueId = processId
                            }
                        });
                    }).Wrap();

                    tasks.Add(t);
                }

                Task.WaitAll(tasks.ToArray());

                Assert.AreEqual(1, tasks.Count(t => t.Result.Status == TaskStatus.RanToCompletion && t.Result.Result == ProcessCreationOutcome.CreatedNew), "Expected only 1 task to have CreatedNew");

                if (mustCreateNewProcess == ProcessCreationOptions.ThrowIfExists)
                    Assert.AreEqual(concurrencyCount - 1, tasks.Count(t => t.Result.Status == TaskStatus.Faulted), "Expected all other tasks to fail");
                else
                    Assert.AreEqual(concurrencyCount - 1, tasks.Count(t => t.Result.Result == ProcessCreationOutcome.AlreadyExists), "Expected all other tasks to be AlreadyExists");
            }
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void DuplicateProcesses_ThrowsExpectedException()
        {
            using (var cluster = CreateTestCluster())
            {
                const string procId = "asiejfiwaj";
                var procInfo = new ProcessCreationRequest
                {
                    Options = ProcessCreationOptions.ThrowIfExists,
                    ProcessInfo = new ProcessCreationInfo { ProcessUniqueId = procId }
                };

                Unwrap(cluster.ProcessBroker.CreateProcess(procInfo));

                UnwrapException(cluster.ProcessBroker.CreateProcess(procInfo), (ProcessAlreadyExistsException ex) =>
                {
                    Assert.AreEqual(procId, ex.ProcessId);
                    Assert.IsTrue(ex.ToString().Contains(procId));
                });
            }
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void DuplicateEndpoints_ThrowsExpectedException()
        {
            using (var cluster = CreateTestCluster())
            {
                const string procId = "asiejfiwaj";
                const string epId = "asijdfjigij";

                var procInfo = new ProcessCreationRequest
                {
                    Options = ProcessCreationOptions.ContinueIfExists,
                    ProcessInfo = new ProcessCreationInfo { ProcessUniqueId = procId, ProcessKind = SimpleIsolationKind }
                };

                var epInfo = new EndpointCreationRequest
                {
                    EndpointId = epId,
                    ImplementationType = typeof(TestInterface),
                    EndpointType = typeof(ITestInterface),
                    Options = ProcessCreationOptions.ThrowIfExists
                };

                Unwrap(cluster.ProcessBroker.CreateProcessAndEndpoint(procInfo, epInfo));
                UnwrapException(cluster.ProcessBroker.CreateProcessAndEndpoint(procInfo, epInfo),
                    (EndpointAlreadyExistsException ex) =>
                {
                    Assert.AreEqual(epId, ex.EndpointId);
                    Assert.IsTrue(ex.ToString().Contains(epId));
                });
            }
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void ConcurrentCreateProcessAndEndpoint_Throw() => ConcurrentCreateProcessAndEndpoint(ProcessCreationOptions.ThrowIfExists);
        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        public void ConcurrentCreateProcessAndEndpoint_NoThrow() => ConcurrentCreateProcessAndEndpoint(ProcessCreationOptions.ContinueIfExists);

        public void ConcurrentCreateProcessAndEndpoint(ProcessCreationOptions mustCreateNewProcess)
        {
            using (var cluster = CreateTestCluster())
            {
                var tasks = new List<Task<Task<ProcessAndEndpointCreationOutcome>>>();
                var processId = "asdfasdf";

                const int concurrencyCount = 10;

                for (int i = 0; i < concurrencyCount; ++i)
                {
                    int innerI = i;

                    tasks.Add(Task.Run(async () =>
                    {
                        var endpointId = "test_" + innerI;
                        var result = await cluster.ProcessBroker.CreateProcessAndEndpoint(new ProcessCreationRequest
                        {
                            Options = mustCreateNewProcess,
                            ProcessInfo = new ProcessCreationInfo
                            {
                                ProcessUniqueId = processId
                            }
                        }, new EndpointCreationRequest
                        {
                            Options = ProcessCreationOptions.ThrowIfExists,
                            EndpointId = endpointId,
                            EndpointType = typeof(ITestInterface),
                            ImplementationType = typeof(LongInitEndpoint)
                        });

                        var ep = cluster.PrimaryProxy.CreateInterface<ITestInterface>($"/{processId}/{endpointId}");
                        Assert.AreEqual(innerI, await ep.Echo(innerI));

                        return result;
                    }).Wrap());
                }

                Task.WaitAll(tasks.ToArray());

                Assert.AreEqual(1, tasks.Count(t => t.Result.Status == TaskStatus.RanToCompletion && t.Result.Result.ProcessOutcome == ProcessCreationOutcome.CreatedNew), "Expected only 1 task to have CreatedNew");

                if (mustCreateNewProcess == ProcessCreationOptions.ThrowIfExists)
                {
                    Assert.AreEqual(concurrencyCount - 1, tasks.Count(t => t.Result.Status == TaskStatus.Faulted), "Expected all other tasks to fail");
                }
                else
                {
                    Assert.AreEqual(concurrencyCount - 1, tasks.Count(t => t.Result.Result.ProcessOutcome == ProcessCreationOutcome.AlreadyExists), "Expected all other tasks to be AlreadyExists");

                    foreach (var t in tasks)
                    {
                        Assert.AreEqual(ProcessCreationOutcome.CreatedNew, t.Result.Result.EndpointOutcome);
                    }
                }
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
            var test = CreateSuccessfulSubprocess(cluster, proc => proc.ProcessKind = processKind);

            testServices.Add(test);

            string targetProcess = WellKnownEndpoints.MasterProcessUniqueId;
            if (!callbackInMaster)
            {
                var test2 = CreateSuccessfulSubprocess(cluster, proc => proc.ProcessKind = processKind);
                testServices.Add(test2);
                targetProcess = ProcessProxy.GetEndpointAddress(test2.TestInterface).TargetProcess;
            }

            var targetEndpointBroker = cluster.PrimaryProxy.CreateInterface<IEndpointBroker>($"/{targetProcess}/{WellKnownEndpoints.EndpointBroker}");

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
                    ProcessKind = DefaultProcessKind,
                    ManuallyRedirectConsole = IsInMsTest
                }
            };

            requestCustomization?.Invoke(req.ProcessInfo);

            var expectedProcessKind = req.ProcessInfo.ProcessKind;
            var requestedRuntime = req.ProcessInfo.SpecificRuntimeVersion;
            if (!string.IsNullOrWhiteSpace(requestedRuntime))
            {
                if (HostFeaturesHelper.GetBestNetcoreRuntime(requestedRuntime) == null)
                    Assert.Ignore(".net core runtime " + requestedRuntime + " is not supported by this host");
            }

            if (!HostFeaturesHelper.IsProcessKindSupported(expectedProcessKind))
                Assert.Ignore("ProcessKind " + expectedProcessKind + " is not supported by this host");
            
            var expectedProcessName = req.ProcessInfo.ProcessName;
            if (string.IsNullOrWhiteSpace(expectedProcessName)
                && !expectedProcessKind.IsFakeProcess()
                && !expectedProcessKind.IsNetcore())
                expectedProcessName = GenericChildProcessHandle.GetDefaultExecutableFileName(expectedProcessKind, ProcessClusterConfiguration.Default);

            Log("CreateProcess...");
            var createdNew = Unwrap(cluster.ProcessBroker.CreateProcess(req));
            Assert.AreEqual(ProcessCreationOutcome.CreatedNew, createdNew);

            var processInfo = Unwrap(cluster.ProcessBroker.GetProcessInformation(procId));
            Assert.AreEqual(procId, processInfo.ProcessName);
            Assert.AreEqual(expectedProcessKind, processInfo.ProcessKind);
            Assert.AreNotEqual(0, processInfo.OsPid);

            var iface = CreateAndValidateTestInterface(cluster, ProcessEndpointAddress.Parse($"/{procId}/"));

            Log("Doing basic validation on target process..");

            if (expectedProcessName != null)
                Assert.AreEqual(expectedProcessName, Unwrap(iface.GetActualProcessName()));

            int expectedPtrSize;

            OsKind expectedOsKind;

            if (expectedProcessKind == ProcessKind.Wsl)
            {
                expectedProcessKind = ProcessKind.Netcore;
                expectedOsKind = OsKind.Linux;
            }
            else
            {
                expectedOsKind = HostFeaturesHelper.LocalMachineOsKind;
            }

            if (expectedProcessKind.IsFakeProcess())
            {
                expectedPtrSize = IntPtr.Size;
                expectedProcessKind = HostFeaturesHelper.LocalProcessKind;
            }
            else
            {
                expectedPtrSize = expectedProcessKind.Is32Bit() ? 4 : 8;
            }

            var processObject = Process.GetProcessById(processInfo.OsPid);

            Log("GetRealProcessKind");
            Assert.AreEqual(expectedProcessKind, Unwrap(iface.GetRealProcessKind()));
            Log("GetOsKind");
            Assert.AreEqual(expectedOsKind, Unwrap(iface.GetOsKind()));
            Log("GetPointerSize");
            Assert.AreEqual(expectedPtrSize, Unwrap(iface.GetPointerSize()));

            if (!string.IsNullOrWhiteSpace(requestedRuntime))
            {
                var ver = Unwrap(iface.GetNetCoreVersion());
                var requestedVersion = new Version(requestedRuntime);
                Assert.AreEqual(requestedVersion.Major, ver.Major, "Unexpected runtime version");
                Assert.AreEqual(requestedVersion.Minor, ver.Minor, "Unexpected runtime version");
            }

            Log("CreateSuccessfulSubprocess success");

            return new TestInterfaceWrapper(cluster, processObject, iface);
        }

        private ITestInterface CreateAndValidateTestInterface(ProcessCluster cluster, ProcessEndpointAddress processEndpointAddress)
        {
            var testEndpoint = Guid.NewGuid().ToString("N");

            var endpointBroker = cluster.PrimaryProxy.CreateInterface<IEndpointBroker>(processEndpointAddress.Combine(WellKnownEndpoints.EndpointBroker));
            endpointBroker.CreateEndpoint(testEndpoint, typeof(ITestInterface), typeof(TestInterface));
            Log("Create Endpoint " + testEndpoint);

            var testInterface = cluster.PrimaryProxy.CreateInterface<ITestInterface>(processEndpointAddress.Combine(testEndpoint));
            var res = Unwrap(testInterface.GetDummyValue());
            DummyReturn.Verify(res);
            Log("Validated Endpoint " + testEndpoint);

            return testInterface;
        }
    }
}
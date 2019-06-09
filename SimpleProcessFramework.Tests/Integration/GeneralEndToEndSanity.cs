using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Server;
using Spfx.Runtime.Server.Processes;
using Spfx.Serialization;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
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

        private const ProcessKind DefaultProcessKind = ProcessKind.Netfx;

        private ProcessCluster m_cluster;

        public interface ITestInterface
        {
            Task<DummyReturn> GetDummyValue(ReflectedTypeInfo exceptionToThrow = default, TimeSpan delay = default, CancellationToken ct = default, string exceptionText = null);
            Task<string> GetActualProcessName();
            Task<int> GetPointerSize();
            Task<string> GetEnvironmentVariable(string key);
            Task<int> Callback(string uri, int num);
            Task<ProcessKind> GetRealProcessKind();
            Task<OsKind> GetOsKind();
            Task<string> GetActualRuntime();
            Task<int> GetProcessId();
        }

        public interface ICallbackInterface
        {
            Task<int> Double(int i);
        }

        private class CallbackInterface : ICallbackInterface
        {
            public Task<int> Double(int i)
            {
                return Task.FromResult(i * 2);
            }
        }

        private class TestInterface : AbstractProcessEndpoint, ITestInterface
        {
            public Task<int> Callback(string uri, int num)
            {
                return ParentProcess.ClusterProxy.CreateInterface<ICallbackInterface>(uri).Double(num);
            }

            public Task<string> GetActualProcessName()
            {
                return Task.FromResult(Process.GetCurrentProcess().ProcessName);
            }

            public Task<string> GetActualRuntime()
            {
                return Task.FromResult(HostFeaturesHelper.CurrentProcessRuntimeDescription);
            }

            public async Task<DummyReturn> GetDummyValue(ReflectedTypeInfo exceptionToThrow, TimeSpan delay, CancellationToken ct, string exceptionText = null)
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, ct);
                }

                if (exceptionToThrow != null)
                {
                    ThrowException_AGgj9a322gj932jyg9a3w4j9(exceptionToThrow, exceptionText);
                }

                return new DummyReturn
                {
                    DummyValue = DummyReturn.ExpectedDummyValue
                };
            }

            public static readonly string ThrowingMethodName = nameof(ThrowException_AGgj9a322gj932jyg9a3w4j9);

            [MethodImpl(MethodImplOptions.NoInlining)]
            private void ThrowException_AGgj9a322gj932jyg9a3w4j9(ReflectedTypeInfo exceptionToThrow, string exceptionText)
            {
                throw (Exception)Activator.CreateInstance(exceptionToThrow.ResolvedType, new object[] { exceptionText ?? "<no exception text>" });
            }

            public Task<string> GetEnvironmentVariable(string key) => Task.FromResult(Environment.GetEnvironmentVariable(key));
            public Task<OsKind> GetOsKind() => Task.FromResult(HostFeaturesHelper.LocalMachineOsKind);
            public Task<int> GetPointerSize() => Task.FromResult(IntPtr.Size);
            public Task<ProcessKind> GetRealProcessKind() => Task.FromResult(HostFeaturesHelper.LocalProcessKind);
            public Task<int> GetProcessId() => Task.FromResult(Process.GetCurrentProcess().Id);
        }

        [SetUp]
        public void Init()
        {
            m_cluster = new ProcessCluster(new ProcessClusterConfiguration
            {
                SupportFakeProcesses = true
            });
        }

        [TearDown]
        public void Cleanup()
        {
            if (m_cluster is null)
                return;

            Log("Invoking cleanup now");
            Unwrap(m_cluster.TeardownAsync(TimeSpan.FromSeconds(30)));
        }

        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void BasicDefaultNameSubprocess() => CreateAndDestroySuccessfulSubprocess();
        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void BasicDefaultNameSubprocess_Netfx() => CreateAndDestroySuccessfulSubprocess(p => p.ProcessKind = ProcessKind.Netfx);
        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void BasicDefaultNameSubprocess_Netfx32() => CreateAndDestroySuccessfulSubprocess(p => p.ProcessKind = ProcessKind.Netfx32);
        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void BasicDefaultNameSubprocess_NetCore() => CreateAndDestroySuccessfulSubprocess(p => p.ProcessKind = ProcessKind.Netcore);
        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void BasicDefaultNameSubprocess_NetCore32() => CreateAndDestroySuccessfulSubprocess(p => p.ProcessKind = ProcessKind.Netcore32);

#if WINDOWS_BUILD
        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void BasicDefaultNameSubprocess_Wsl() => CreateAndDestroySuccessfulSubprocess(p => p.ProcessKind = ProcessKind.Wsl);
#endif

        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void BasicTestInMasterProcess() => CreateAndValidateTestInterface(m_cluster.MasterProcess.UniqueAddress);
        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void BasicProcessCallbackToMaster() => TestCallback(DefaultProcessKind);
        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void FakeProcessCallbackToMaster() => TestCallback(ProcessKind.DirectlyInRootProcess);

#if WINDOWS_BUILD && NETFX_BUILD
        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void AppDomainCallbackToOtherProcess() => TestCallback(ProcessKind.AppDomain, callbackInMaster: false);
#endif

        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void FakeProcessCallbackToOtherProcess() => TestCallback(ProcessKind.DirectlyInRootProcess, callbackInMaster: false);
        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void BasicProcessCallbackToOtherProcess() => TestCallback(DefaultProcessKind, callbackInMaster: false);
        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void BasicNetcore_Runtime21() => CreateAndDestroySuccessfulSubprocess(p => { p.ProcessKind = ProcessKind.Netcore; p.SpecificRuntimeVersion = "2.1"; });
        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void BasicNetcore_Runtime30() => CreateAndDestroySuccessfulSubprocess(p => { p.ProcessKind = ProcessKind.Netcore; p.SpecificRuntimeVersion = "3.0"; });

        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void BasicEnvironmentVariableSubprocess()
        {
            var envVar = "AWGJIEAJWIGJIAWE";
            var envValue = "JIAEWIGJEWIGHJRIEHJI";
            var iface = CreateSuccessfulSubprocess(procInfo =>
            {
                procInfo.ExtraEnvironmentVariables = new[] { new ProcessCreationInfo.KeyValuePair(envVar, envValue) }.ToList();
            });

            using (iface)
            {
                Assert.AreEqual(envValue, Unwrap(iface.TestInterface.GetEnvironmentVariable(envVar)));
            }
        }

        [Test, Timeout(DefaultTestTimeout), Parallelizable]
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

        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void ThrowCustomException()
        {
            using (var svc = CreateSuccessfulSubprocess(p => p.ProcessKind = ProcessKind.DirectlyInRootProcess))
            {
                var text = Guid.NewGuid().ToString("N");
                UnwrapException(svc.TestInterface.GetDummyValue(typeof(CustomExceptionNotMarshalled), exceptionText: text), typeof(RemoteException), expectedText: text, expectedStackFrame: TestInterface.ThrowingMethodName);
            }
        }

        private void TestCallback(ProcessKind processKind, bool callbackInMaster = true)
        {
            var testServices = new List<TestInterfaceWrapper>();
            var test = CreateSuccessfulSubprocess(proc => proc.ProcessKind = processKind);

            testServices.Add(test);

            string targetProcess = WellKnownEndpoints.MasterProcessUniqueId;
            if (!callbackInMaster)
            {
                var test2 = CreateSuccessfulSubprocess(proc => proc.ProcessKind = processKind);
                testServices.Add(test2);
                targetProcess = ProcessProxy.GetEndpointAddress(test2.TestInterface).TargetProcess;
            }

            var targetEndpointBroker = m_cluster.PrimaryProxy.CreateInterface<IEndpointBroker>($"/{targetProcess}/{WellKnownEndpoints.EndpointBroker}");

            var callbackEndpoint = Guid.NewGuid().ToString("N");

            Unwrap(targetEndpointBroker.CreateEndpoint(new EndpointCreationRequest
            {
                EndpointId = callbackEndpoint,
                EndpointType = typeof(ICallbackInterface),
                ImplementationType = typeof(CallbackInterface),
                FailIfExists = true
            }));

            var input = 5;
            var res = Unwrap(test.TestInterface.Callback($"/{targetProcess}/{callbackEndpoint}", input));
            Assert.AreEqual(input * 2, res);

            foreach(var svc in testServices)
            {
                svc.Dispose();
            }
        }

        private void CreateAndDestroySuccessfulSubprocess(Action<ProcessCreationInfo> requestCustomization = null)
        {
            CreateSuccessfulSubprocess(requestCustomization).Dispose();
        }

        private void DisposeTestProcess(ITestInterface svc)
        {
            var processKind = Unwrap(svc.GetRealProcessKind());

            var pid = Unwrap(svc.GetProcessId());
            var proc = Process.GetProcessById(pid);
            var processName = ProcessProxy.GetEndpointAddress(svc).TargetProcess;

            Unwrap(m_cluster.ProcessBroker.DestroyProcess(processName));

            if (processKind != ProcessKind.Wsl)
                proc.WaitForExit(DefaultTestTimeout);
        }

        public class TestInterfaceWrapper : IDisposable
        {
            public ITestInterface TestInterface { get; }

            private readonly GeneralEndToEndSanity m_owner;

            public TestInterfaceWrapper(GeneralEndToEndSanity owner, ITestInterface iface)
            {
                TestInterface = iface;
                m_owner = owner;
            }

            public void Dispose()
            {
                m_owner.DisposeTestProcess(TestInterface);
            }
        }

        private TestInterfaceWrapper CreateSuccessfulSubprocess(Action<ProcessCreationInfo> requestCustomization = null)
        {
            var procId = Guid.NewGuid().ToString("N");
            var req = new ProcessCreationRequest
            {
                MustCreateNew = true,
                ProcessInfo = new ProcessCreationInfo
                {
                    ProcessUniqueId = procId,
                    ProcessKind = DefaultProcessKind,
                    ManuallyRedirectConsole = IsInMsTest
                }
            };

            requestCustomization?.Invoke(req.ProcessInfo);

            var requestedRuntime = req.ProcessInfo.SpecificRuntimeVersion;
            if (!string.IsNullOrWhiteSpace(requestedRuntime))
            {
                if (HostFeaturesHelper.GetBestNetcoreRuntime(requestedRuntime) == null)
                    Assert.Inconclusive(".net core runtime " + requestedRuntime + " is not supported by this host");
            }

            if (!HostFeaturesHelper.IsProcessKindSupported(req.ProcessInfo.ProcessKind))
                Assert.Inconclusive("ProcessKind " + req.ProcessInfo.ProcessKind + " is not supported by this host");

            var expectedProcessKind = req.ProcessInfo.ProcessKind;

            var expectedProcessName = req.ProcessInfo.ProcessName;
            if (string.IsNullOrWhiteSpace(expectedProcessName)
                && !expectedProcessKind.IsFakeProcess()
                && !expectedProcessKind.IsNetcore())
                expectedProcessName = GenericChildProcessHandle.GetDefaultExecutableFileName(expectedProcessKind, ProcessClusterConfiguration.Default);

            Log("CreateProcess...");
            var createdNew = Unwrap(m_cluster.ProcessBroker.CreateProcess(req));
            Assert.AreEqual(ProcessCreationOutcome.CreatedNew, createdNew);

            var iface = CreateAndValidateTestInterface(ProcessEndpointAddress.Parse($"/{procId}/"));

            Log("Doing basic validation on target process..");

            if (expectedProcessName != null)
                Assert.AreEqual(expectedProcessName, Unwrap(iface.GetActualProcessName()));

            int expectedPtrSize;

            OsKind expectedOsKind;

            if(expectedProcessKind == ProcessKind.Wsl)
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

            Log("GetRealProcessKind");
            Assert.AreEqual(expectedProcessKind, Unwrap(iface.GetRealProcessKind()));
            Log("GetOsKind");
            Assert.AreEqual(expectedOsKind, Unwrap(iface.GetOsKind()));
            Log("GetPointerSize");
            Assert.AreEqual(expectedPtrSize, Unwrap(iface.GetPointerSize()));

            /*
             * TODO - 3.0 preview returns 2.1 :/
             * 
             * if (!string.IsNullOrWhiteSpace(requestedRuntime))
            {
                var m = Regex.Match(Unwrap(iface.GetActualRuntime()), @"v(?<v>(\d|\.)+)");
                var ver = m.Groups["v"].Value;
                Assert.IsTrue(ver.Contains(requestedRuntime) || requestedRuntime.Contains(ver), $"Expected runtime version {requestedRuntime} actual {ver}");
            }*/

            Log("CreateSuccessfulSubprocess success");

            return new TestInterfaceWrapper(this, iface);
        }

        private ITestInterface CreateAndValidateTestInterface(ProcessEndpointAddress processEndpointAddress)
        {
            var testEndpoint = Guid.NewGuid().ToString("N");

            var endpointBroker = m_cluster.PrimaryProxy.CreateInterface<IEndpointBroker>(processEndpointAddress.Combine(WellKnownEndpoints.EndpointBroker));
            endpointBroker.CreateEndpoint(testEndpoint, typeof(ITestInterface), typeof(TestInterface));
            Log("Create Endpoint " + testEndpoint);

            var testInterface = m_cluster.PrimaryProxy.CreateInterface<ITestInterface>(processEndpointAddress.Combine(testEndpoint));
            var res = Unwrap(testInterface.GetDummyValue());
            DummyReturn.Verify(res);
            Log("Validated Endpoint " + testEndpoint);

            return testInterface;
        }
    }
}

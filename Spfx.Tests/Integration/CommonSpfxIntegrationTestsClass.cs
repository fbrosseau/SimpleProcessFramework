using FluentAssertions;
using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Runtime.Server.Listeners;
using Spfx.Tests.Utilities;
using Spfx.Utilities;
using Spfx.Utilities.Runtime;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Tests.Integration
{
    public delegate void ClusterCustomizationDelegate(ProcessClusterConfiguration config);

    public abstract class CommonSpfxIntegrationTestsClass : CommonSpfxTestsClass
    {
        private readonly SanityTestOptions m_options;
        private static long s_nextUniqueId;

        protected CommonSpfxIntegrationTestsClass(SanityTestOptions options = SanityTestOptions.Interprocess)
        {
            m_options = options;
        }

        public override async ValueTask ClassSetUp()
        {
            await base.ClassSetUp().WT();

            try
            {
                await NetcoreInfo.InitializeInstalledVersionsAsync().WT();
                // this is just to run those constructors first
                if (NetcoreInfo.X86.IsSupported)
                    await NetcoreInfo.InitializeInstalledVersionsAsync(x86: true).WT();
            }
            catch
            {
            }
        }

        protected void CreateAndDestroySuccessfulSubprocess(Action<ProcessCreationInfo> requestCustomization = null, ClusterCustomizationDelegate customConfig = null)
        {
            using var cluster = CreateTestCluster(customConfig);
            CreateSuccessfulSubprocess(cluster, requestCustomization).Dispose();
        }

        protected ProcessCluster CreateTestCluster(ClusterCustomizationDelegate customConfig = null, bool withTcp = false)
        {
            var config = new ProcessClusterConfiguration
            {
                EnableFakeProcesses = true,
                EnableAppDomains = true,
                EnableWsl = true,
                Enable32Bit = true,
                TypeResolverFactoryType = typeof(TestTypeResolverFactory),
                ConsoleProvider = new TestConsoleProvider()
            };

            customConfig?.Invoke(config);

            var cluster = new ProcessCluster(config);

            if ((m_options & SanityTestOptions.Tcp) != 0 || withTcp)
                cluster.AddListener(new TcpInterprocessConnectionListener(0));

            var exceptionHandler = new ExceptionReportingEndpoint();
            cluster.MasterProcess.InitializeEndpointAsync<IExceptionReportingEndpoint>(ExceptionReportingEndpoint.EndpointId, exceptionHandler).FireAndForget();

            return cluster;
        }

        protected TestInterfaceWrapper CreateSuccessfulSubprocess(Action<ProcessCreationInfo> requestCustomization = null, ClusterCustomizationDelegate customConfig = null)
        {
            var cluster = CreateTestCluster(customConfig);
            var iface = CreateSuccessfulSubprocess(cluster, requestCustomization);
            iface.AddExtraDisposable(cluster);
            return iface;
        }

        protected TestInterfaceWrapper CreateSuccessfulSubprocess(ProcessCluster cluster, Action<ProcessCreationInfo> requestCustomization = null)
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
                if (NetcoreInfo.GetBestNetcoreRuntime(netcore) == null)
                    Assert.Fail($".net core runtime {requestedRuntime} is not supported by this host. The supported runtimes are: \r\n"
                        + string.Join("\r\n", NetcoreInfo.GetSupportedRuntimes(netcore.ProcessKind).Select(r => "- " + r)));
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
            Assert.AreEqual(ProcessCreationResults.CreatedNew, createdNew);

            var processInfo = Unwrap(cluster.ProcessBroker.GetProcessInformation(procId));
            Assert.AreEqual(procId, processInfo.ProcessName);
            Assert.AreEqual(expectedProcessKind, processInfo.Framework.ProcessKind);
            Assert.AreNotEqual(0, processInfo.OsPid);

            var iface = CreateAndValidateTestInterface(cluster, ProcessEndpointAddress.Parse($"/{procId}/"));

            Log("Doing basic validation on target process..");

            bool expectDotNetExe = expectedProcessName?.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) == true;

            if (expectedProcessKind.IsNetcore())
            {
                var ver = Unwrap(iface.GetNetCoreVersion());
                expectDotNetExe |= ver.Major < 3
                    || (expectedProcessName != null
                        && SharedTestcustomHostUtilities.IsCustomHostAssembly(expectedProcessName, includeDll: true, includeExe: false));

                if (!string.IsNullOrWhiteSpace(targetNetcoreRuntime))
                {
                    var requestedVersion = NetcoreInfo.ParseNetcoreVersion(targetNetcoreRuntime);
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
                if (!realProcName.EndsWith("dotnet", StringComparison.OrdinalIgnoreCase))
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

        protected ITestInterface CreateAndValidateTestInterface(ProcessCluster cluster, ProcessEndpointAddress processEndpointAddress)
        {
            var testEndpoint = "EP_" + GetNextUniqueId();

            var proxy = CreateProxy(cluster);

            var endpointBroker = CreateProxyInterface<IEndpointBroker>(proxy, cluster, processEndpointAddress.ProcessId, WellKnownEndpoints.EndpointBroker);
            Unwrap(endpointBroker.CreateEndpoint(testEndpoint, typeof(ITestInterface), typeof(TestInterface)));
            Log("Create Endpoint " + testEndpoint);

            var testInterface = CreateProxyInterface<ITestInterface>(proxy, cluster, processEndpointAddress.ProcessId, testEndpoint);
            var res = Unwrap(testInterface.GetDummyValue());
            TestReturnValue.Verify(res);
            Log("Validated Endpoint " + testEndpoint);

            string testArg = Guid.NewGuid().ToString();
            var receivedArg = new TaskCompletionSource<string>();

            EventHandler<TestEventArgs> h = (sender, e) =>
            {
                try
                {
                    receivedArg.TrySetResult(e.Arg as string);
                }
                catch (Exception ex)
                {
                    receivedArg.TrySetException(ex);
                }
            };

            try
            {
                Unwrap(ProcessProxy.SubscribeEventsAsync(() => testInterface.TestEvent += h));
                Unwrap(testInterface.RaiseEvent(testArg));
                Unwrap(receivedArg.Task);
                receivedArg.Task.Result.Should().Be(testArg);
            }
            finally
            {
                testInterface.TestEvent -= h;
            }

            return testInterface;
        }

        protected string GetNextUniqueId()
        {
            return Interlocked.Increment(ref s_nextUniqueId).ToString("X4");
        }

        /*
        public class ITestLogListener
        {

        }

        private class TestLogListener : ILogListener
        {

        }*/

        protected ProcessProxy CreateProxy(ProcessCluster cluster)
        {
            if ((m_options & SanityTestOptions.Interprocess) != 0)
                return cluster.PrimaryProxy;
            return new ProcessProxy(encryptConnections: false);
        }

        protected T CreateProxyInterface<T>(ProcessCluster cluster, string processId, string endpointId)
        {
            return CreateProxyInterface<T>(CreateProxy(cluster), cluster, processId, endpointId);
        }

        protected T CreateProxyInterface<T>(ProcessProxy proxy, ProcessCluster cluster, string processId, string endpointId)
        {
            if ((m_options & SanityTestOptions.Interprocess) != 0)
            {
                return proxy.CreateInterface<T>($"/{processId}/{endpointId}");
            }
            else
            {
                var ep = cluster.GetListenEndpoints().OfType<IPEndPoint>().First();
                if (ep.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (ep.Address.Equals(IPAddress.Any))
                        ep = new IPEndPoint(IPAddress.Loopback, ep.Port);
                }
                else
                {
                    if (ep.Address.Equals(IPAddress.IPv6Any))
                        ep = new IPEndPoint(IPAddress.IPv6Loopback, ep.Port);
                }

                var addr = ProcessEndpointAddress.Create(ep.ToString(), processId, endpointId);
                return proxy.CreateInterface<T>(addr);
            }
        }

        internal static async Task DisposeTestProcess(TestInterfaceWrapper testInterface, CancellationToken ct)
        {
            var svc = testInterface.TestInterface;
            var proc = testInterface.ProcessInfo;
            var pid = proc.Id;

            var addr = ProcessProxy.GetEndpointAddress(svc);
            var processName = addr.ProcessId;

            try
            {
                Log("Invoking DestroyEndpoint for " + addr.EndpointId);
                await testInterface.Cluster.PrimaryProxy.DestroyEndpoint(addr).WT();
            }
            catch (Exception ex)
            {
                Log("DestroyEndpoint failed: " + ex.Message);
            }

            try
            {
                Log("Invoking DestroyProcess for " + processName);
                await testInterface.Cluster.ProcessBroker.DestroyProcess(processName).WT();
            }
            catch (Exception ex)
            {
                Log("DestroyProcess failed: " + ex.Message);
            }

            if (pid != ProcessUtilities.CurrentProcessId)
            {
                Log("Waiting for real exit of process" + pid);
                await proc.WaitForExitAsync(DefaultTestTimeoutTimespan, ct).WT();
            }
        }

        public class TestInterfaceWrapper : AsyncDestroyable
        {
            public ITestInterface TestInterface { get; }
            public Process ProcessInfo { get; }
            public ProcessCluster Cluster { get; }
            public string PhysicalProcessName { get; }
            public bool CanWaitForExit { get; }
            private readonly List<IDisposable> m_objectsToDisposeLast = new List<IDisposable>();
            private readonly List<IDisposable> m_objectsToDisposeFirst = new List<IDisposable>();

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

            protected override async ValueTask OnTeardownAsync(CancellationToken ct = default)
            {
                static async ValueTask DisposeAsync(IDisposable d)
                {
                    if (d is IAsyncDisposable a)
                        await a.DisposeAsync().WT();
                    else
                        d.Dispose();
                }

                foreach (var f in m_objectsToDisposeFirst)
                {
                    await DisposeAsync(f).WT();
                }
                await DisposeTestProcess(this, ct).WT();
                foreach (var l in m_objectsToDisposeLast)
                {
                    await DisposeAsync(l).WT();
                }

                await base.OnTeardownAsync(ct).WT();
            }

            protected override void OnDispose()
            {
                foreach (var f in m_objectsToDisposeFirst)
                {
                    f.Dispose();
                }
                Unwrap(DisposeTestProcess(this, default));
                foreach (var l in m_objectsToDisposeLast)
                {
                    l.Dispose();
                }
            }

            internal void AddExtraDisposable(IDisposable disposable, bool processLast = true)
            {
                (processLast ? m_objectsToDisposeLast : m_objectsToDisposeFirst).Add(disposable);
            }
        }
    }
}
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimpleProcessFramework.Interfaces;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Server.Processes;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SimpleProcessFramework.Tests.TestUtilities;
using SimpleProcessFramework.Utilities.Threading;

namespace SimpleProcessFramework.Tests.Integration
{
    [TestClass]
    public class GeneralEndToEndSanity
    {
        private ProcessCluster m_cluster;

        public interface ITestInterface
        {
            Task<DummyReturn> GetDummyValue(ReflectedTypeInfo exceptionToThrow = default, TimeSpan delay = default, CancellationToken ct = default);
            Task<string> GetActualProcessName();
            Task<string> GetEnvironmentVariable(string key);
        }

        private class TestInterface : ITestInterface
        {
            public Task<string> GetActualProcessName()
            {
                return Task.FromResult(Process.GetCurrentProcess().ProcessName);
            }

            public async Task<DummyReturn> GetDummyValue(ReflectedTypeInfo exceptionToThrow, TimeSpan delay, CancellationToken ct)
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, ct);
                }

                if (exceptionToThrow != null)
                {
                    throw (Exception)Activator.CreateInstance(exceptionToThrow.ResolvedType);
                }

                return new DummyReturn
                {
                    DummyValue = DummyReturn.ExpectedDummyValue
                };
            }

            public Task<string> GetEnvironmentVariable(string key)
            {
                return Task.FromResult(Environment.GetEnvironmentVariable(key));
            }
        }

        [TestInitialize]
        public void Init()
        {
            m_cluster = new ProcessCluster();
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (m_cluster is null)
                return;

            m_cluster.TeardownAsync(TimeSpan.FromSeconds(14564)).Wait();
        }

        [TestMethod]
        public void BasicDefaultNameSubprocess()
        {
            CreateSuccessfulSubprocess();
        }

        [TestMethod]
        public void BasicEnvironmentVariableSubprocess()
        {
            var envVar = "AWGJIEAJWIGJIAWE";
            var envValue = "JIAEWIGJEWIGHJRIEHJI";
            var iface = CreateSuccessfulSubprocess(procInfo =>
            {
                procInfo.ExtraEnvironmentVariables = new[] { new ProcessCreationInfo.KeyValuePair(envVar, envValue) }.ToList();
            });

            Assert.AreEqual(envValue, Unwrap(iface.GetEnvironmentVariable(envVar)));
        }

        [TestMethod]
        public void BasicCustomNameSubprocess()
        {
            const string customProcessName = "SPFW.UnitTests.CustomProcess_agj90gj09jg0a94jg094jg";

            DeleteFileIfExists(customProcessName + ".exe");
            DeleteFileIfExists(customProcessName + ".dll");

            CreateSuccessfulSubprocess(procInfo =>
            {
                procInfo.ProcessName = customProcessName;
            });
        }

        [TestMethod]
        public void BasicTestInMasterProcess()
        {
            CreateAndValidateTestInterface(m_cluster.MasterProcess.UniqueAddress);
        }

        private ITestInterface CreateSuccessfulSubprocess(Action<ProcessCreationInfo> requestCustomization =  null)
        {
            var procId = Guid.NewGuid().ToString("N");
            var req = new ProcessCreationRequest
            {
                MustCreateNew = true,
                ProcessInfo = new ProcessCreationInfo
                {
                    ProcessUniqueId = procId,
                    ProcessKind = ProcessKind.Netfx // TODO - shouldn't be required
                }
            };

            requestCustomization?.Invoke(req.ProcessInfo);

            var expectedProcessName = req.ProcessInfo.ProcessName;
            if (string.IsNullOrWhiteSpace(expectedProcessName))
                expectedProcessName = GenericChildProcessHandle.GetDefaultExecutableFileName(req.ProcessInfo.ProcessKind, ProcessClusterConfiguration.Default);

            var createdNew = Unwrap(m_cluster.ProcessBroker.CreateProcess(req));
            Assert.IsTrue(createdNew);

            var iface = CreateAndValidateTestInterface(ProcessEndpointAddress.Parse($"/{procId}/"));

            Assert.AreEqual(expectedProcessName, Unwrap(iface.GetActualProcessName()));

            return iface;
        }

        private ITestInterface CreateAndValidateTestInterface(ProcessEndpointAddress processEndpointAddress)
        {
            var testEndpoint = Guid.NewGuid().ToString("N");
            var endpointBroker = m_cluster.PrimaryProxy.CreateInterface<IEndpointBroker>(processEndpointAddress.Combine(WellKnownEndpoints.EndpointBroker));
            endpointBroker.CreateEndpoint(testEndpoint, typeof(ITestInterface), typeof(TestInterface));

            var testInterface = m_cluster.PrimaryProxy.CreateInterface<ITestInterface>(processEndpointAddress.Combine(testEndpoint));
            var res = Unwrap(testInterface.GetDummyValue());
            DummyReturn.Verify(res);

            return testInterface;
        }
    }
}

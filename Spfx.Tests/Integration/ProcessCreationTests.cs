using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Runtime.Exceptions;
using Spfx.Utilities.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Spfx.Tests.Integration
{
    [Parallelizable(ParallelScope.Children)]
    public class ProcessCreationTests : CommonSpfxIntegrationTestsClass
    {
        private class LongInitEndpoint : TestInterface
        {
            protected override async ValueTask InitializeAsync()
            {
                await Task.Delay(1000);
                await base.InitializeAsync();
            }
        }

        [Test]
        public void ConcurrentCreateProcess_Throw() => ConcurrentCreateProcess(ProcessCreationOptions.ThrowIfExists);
        [Test]
        public void ConcurrentCreateProcess_NoThrow() => ConcurrentCreateProcess(ProcessCreationOptions.ContinueIfExists);

        private void ConcurrentCreateProcess(ProcessCreationOptions mustCreateNewProcess)
        {
            using var cluster = CreateTestCluster();

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

            Assert.AreEqual(1, tasks.Count(t => t.Result.IsCompletedSuccessfully() && t.Result.Result == ProcessCreationOutcome.CreatedNew), "Expected only 1 task to have CreatedNew");

            if (mustCreateNewProcess == ProcessCreationOptions.ThrowIfExists)
                Assert.AreEqual(concurrencyCount - 1, tasks.Count(t => t.Result.Status == TaskStatus.Faulted), "Expected all other tasks to fail");
            else
                Assert.AreEqual(concurrencyCount - 1, tasks.Count(t => t.Result.Result == ProcessCreationOutcome.AlreadyExists), "Expected all other tasks to be AlreadyExists");
        }

        [Test]
        public void DuplicateProcesses_ThrowsExpectedException()
        {
            using var cluster = CreateTestCluster();

            const string procId = "asiejfiwaj";
            var procInfo = new ProcessCreationRequest
            {
                Options = ProcessCreationOptions.ThrowIfExists,
                ProcessInfo = new ProcessCreationInfo { ProcessUniqueId = procId }
            };

            Unwrap(cluster.ProcessBroker.CreateProcess(procInfo));

            ExpectException(cluster.ProcessBroker.CreateProcess(procInfo), (ProcessAlreadyExistsException ex) =>
            {
                Assert.AreEqual(procId, ex.ProcessId);
                Assert.IsTrue(ex.ToString().Contains(procId));
            });
        }

        [Test]
        public void DuplicateEndpoints_ThrowsExpectedException()
        {
            using var cluster = CreateTestCluster();

            const string procId = "asiejfiwaj";
            const string epId = "asijdfjigij";

            var procInfo = new ProcessCreationRequest
            {
                Options = ProcessCreationOptions.ContinueIfExists,
                ProcessInfo = new ProcessCreationInfo { ProcessUniqueId = procId, TargetFramework = SimpleIsolationKind }
            };

            var epInfo = new EndpointCreationRequest
            {
                EndpointId = epId,
                ImplementationType = typeof(TestInterface),
                EndpointType = typeof(ITestInterface),
                Options = ProcessCreationOptions.ThrowIfExists
            };

            Unwrap(cluster.ProcessBroker.CreateProcessAndEndpoint(procInfo, epInfo));
            ExpectException(cluster.ProcessBroker.CreateProcessAndEndpoint(procInfo, epInfo),
                (EndpointAlreadyExistsException ex) =>
                {
                    Assert.AreEqual(epId, ex.EndpointId);
                    Assert.IsTrue(ex.ToString().Contains(epId));
                });
        }

        [Test]
        public void ConcurrentCreateProcessAndEndpoint_Throw() => ConcurrentCreateProcessAndEndpoint(ProcessCreationOptions.ThrowIfExists);
        [Test]
        public void ConcurrentCreateProcessAndEndpoint_NoThrow() => ConcurrentCreateProcessAndEndpoint(ProcessCreationOptions.ContinueIfExists);

        public void ConcurrentCreateProcessAndEndpoint(ProcessCreationOptions mustCreateNewProcess)
        {
            using var cluster = CreateTestCluster();

            var tasks = new List<Task<Task<ProcessAndEndpointCreationOutcome>>>();
            var processId = "asdfasdf";

            var proxy = CreateProxy(cluster);

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

                    var ep = CreateProxyInterface<ITestInterface>(proxy, cluster, processId, endpointId);
                    Assert.AreEqual(innerI, await ep.Echo(innerI));

                    return result;
                }).Wrap());
            }

            Task.WaitAll(tasks.ToArray());

            Assert.AreEqual(1, tasks.Count(t => t.Result.IsCompletedSuccessfully() && t.Result.Result.ProcessOutcome == ProcessCreationOutcome.CreatedNew), "Expected only 1 task to have CreatedNew");

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
}

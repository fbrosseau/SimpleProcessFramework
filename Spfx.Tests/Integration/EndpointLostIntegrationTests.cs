using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Server;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Spfx.Tests.Integration
{
    [Parallelizable(ParallelScope.Children)]
    public class EndpointLostIntegrationTests : CommonSpfxIntegrationTestsClass
    {
        [Test]
        public async ValueTask SimpleEndpointLost()
        {
            using var cluster = CreateTestCluster(withTcp: true);

            var proc = "Proc" + GetNextUniqueId();

            var proxy = new ProcessProxy(encryptConnections: false);

            IAnyEndpoint CreateProxy(string processId, string endpointId = null)
            {
                return proxy.CreateInterface<IAnyEndpoint>(
                    ProcessEndpointAddress.Create(
                        cluster.GetConnectEndpoints().First(),
                        processId,
                        endpointId ?? WellKnownEndpoints.EndpointBroker));
            }

            async Task ExpectFailure<TException>(string processId, string endpointId = null)
                where TException : Exception
            {
                await AssertThrowsAsync<TException>(() => ExecuteCall(processId, endpointId));
            }

            async Task ExecuteCall(string processId, string endpointId = null)
            {
                var ep = CreateProxy(processId, endpointId);
                await ProcessProxy.PingAsync(ep);
            }

            for (int i = 0; i < 2; ++i)
            {
                await ExecuteCall(WellKnownEndpoints.MasterProcessUniqueId);
                await ExpectFailure<ProcessNotFoundException>(proc);
                await ExpectFailure<ProcessNotFoundException>(proc, "TestEndpoint");

                await cluster.MasterProcess.ProcessBroker.CreateProcess(new ProcessCreationRequest
                {
                    ProcessInfo = new ProcessCreationInfo { ProcessUniqueId = proc, TargetFramework = TargetFramework.DirectlyInRootProcess }
                });

                await ExecuteCall(proc);
                await ExpectFailure<EndpointNotFoundException>(proc, "TestEndpoint");

                await cluster.MasterProcess.ProcessBroker.CreateProcessAndEndpoint(new ProcessCreationRequest
                {
                    Options = ProcessCreationOptions.ContinueIfExists,
                    ProcessInfo = new ProcessCreationInfo { ProcessUniqueId = proc }
                }, new EndpointCreationRequest
                {
                    EndpointId = "TestEndpoint",
                    ImplementationType = typeof(TestInterface),
                    EndpointType = typeof(ITestInterface)
                });

                await ExecuteCall(proc);
                await ExecuteCall(proc, "TestEndpoint");

                await cluster.MasterProcess.ProcessBroker.DestroyProcess(proc, onlyIfEmpty: false);
            }
        }
    }
}
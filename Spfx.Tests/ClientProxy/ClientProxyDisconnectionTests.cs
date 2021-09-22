using FluentAssertions;
using NUnit.Framework;
using Spfx.Tests.Integration;
using Spfx.Utilities.Threading;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Spfx.Tests.ClientProxy
{
    [TestFixture, Parallelizable(ParallelScope.All)]
    public class ClientProxyDisconnectionTests : CommonSpfxIntegrationTestsClass
    {
        public ClientProxyDisconnectionTests()
            : base(SanityTestOptions.Tcp)
        {
        }

        public enum ProcessDestroyMethods
        {
            SelfDispose,
            RemoteDestroy,
            CluserDispose,
            SavageProcessExit,
            SelfEnvironmentExit
        }

        [Test]
        public ValueTask ClientProxy_OngoingCall_ClusterDispose() => TestCallAbort(ProcessDestroyMethods.CluserDispose);

        [Test]
        public ValueTask ClientProxy_OngoingCall_EndpointSelfDispose() => TestCallAbort(ProcessDestroyMethods.SelfDispose);

        [Test]
        public ValueTask ClientProxy_OngoingCall_EndpointRemoteDestroyEndpoint() => TestCallAbort(ProcessDestroyMethods.RemoteDestroy);

        [Test]
        public ValueTask ClientProxy_OngoingCall_SavageProcessExit() => TestCallAbort(ProcessDestroyMethods.SavageProcessExit);

        [Test]
        public ValueTask ClientProxy_OngoingCall_SelfEnvironmentExit() => TestCallAbort(ProcessDestroyMethods.SelfEnvironmentExit);

        private async ValueTask TestCallAbort(ProcessDestroyMethods method)
        {
            await using var cluster = CreateTestCluster();
            await using var iface = CreateSuccessfulSubprocess(cluster);

            var addr = ProcessProxy.GetEndpointAddress(iface.TestInterface);

            var prox = new ProcessProxy(encryptConnections: false);
            var ifaceProxy = prox.CreateInterface<ITestInterface>(ProcessEndpointAddress.Create(cluster.GetConnectEndpoints().First(), addr.ProcessId, addr.EndpointId));

            // complete a dummy call to ensure the connection is already established
            await ifaceProxy.GetProcessId().WT();

            var dummyCall = ifaceProxy.GetDummyValue(delay: TimeSpan.FromDays(1));

            switch (method)
            {
                case ProcessDestroyMethods.CluserDispose:
                    await iface.DisposeAsync().WT();
                    await cluster.DisposeAsync().WT();
                    break;
                case ProcessDestroyMethods.SelfDispose:
                    await iface.TestInterface.SelfDispose().Wrap().WT(); // this may fail
                    break;
                case ProcessDestroyMethods.RemoteDestroy:
                    await ProcessProxy.DestroyEndpoint(iface.TestInterface).WT();
                    break;
                case ProcessDestroyMethods.SelfEnvironmentExit:
                    await iface.TestInterface.EnvironmentExit().Wrap().WT(); // this may fail
                    break;
                case ProcessDestroyMethods.SavageProcessExit:
                    await iface.TestInterface.SavageExitOwnProcess().Wrap().WT(); // this may fail
                    break;
            }

            (await dummyCall.Wrap().TryWaitAsync(TimeSpan.FromSeconds(10))).Should().Be(true);

            await AssertThrowsAsync(() => dummyCall).WT();
        }
    }
}
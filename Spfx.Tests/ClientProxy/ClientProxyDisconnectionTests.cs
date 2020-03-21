using FluentAssertions;
using Spfx.Utilities.Threading;
using NUnit.Framework;
using Spfx.Runtime.Exceptions;
using Spfx.Tests.Integration;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

        [Test]
        public async ValueTask ClientProxy_OngoingCall_ClusterDispose()
        {
            await using var cluster = CreateTestCluster();
            await using var iface = CreateSuccessfulSubprocess(cluster);

            var addr = ProcessProxy.GetEndpointAddress(iface.TestInterface);

            var prox = new ProcessProxy(encryptConnections: false);
            var ifaceProxy = prox.CreateInterface<ITestInterface>(ProcessEndpointAddress.Create(new IPEndPoint(IPAddress.Loopback, ((IPEndPoint)cluster.GetListenEndpoints().First()).Port), addr.ProcessId, addr.EndpointId));

            // complete a dummy call to ensure the connection is already established
            await ifaceProxy.GetProcessId();

            var dummyCall = ifaceProxy.GetDummyValue(delay: TimeSpan.FromDays(1));

            await iface.DisposeAsync();
            await cluster.DisposeAsync();

            (await dummyCall.Wrap().WaitAsync(TimeSpan.FromSeconds(10))).Should().Be(true);

            await AssertThrowsAsync(() => dummyCall);
        }
    }
}
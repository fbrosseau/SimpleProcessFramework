using FluentAssertions;
using NUnit.Framework;
using Spfx.Runtime.Exceptions;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Spfx.Tests.ClientProxy
{
    [TestFixture, Parallelizable(ParallelScope.All)]
    public class ClientProxyExceptionTests : CommonTestClass
    {
        [Test]
        public ValueTask ClientProxy_ConnectBadPort_ThrowsSocketException()
        {
            return TestBadTcp(new IPEndPoint(IPAddress.Loopback, 62178));
        }

        [Test]
        public ValueTask ClientProxy_ConnectBadAddress_ThrowsSocketException()
        {
            return TestBadTcp(new IPEndPoint(IPAddress.Broadcast, 62178), SocketError.AddressNotAvailable);
        }

        [Test]
        public ValueTask ClientProxy_ConnectUnknownDns_ThrowsSocketException()
        {
            return TestBadTcp(new DnsEndPoint("test.invalid", 62178), SocketError.HostNotFound);
        }

        private ValueTask TestBadTcp(EndPoint ep, SocketError? expectedError = null, Action<ProxySocketConnectionFailedException> extraValidation = null)
        {
            var prox = new ProcessProxy(encryptConnections: false);
            var iface = prox.CreateInterface<ITestInterface>(ProcessEndpointAddress.Create(ep, "A", "A"));
            return AssertThrowsAsync(() => iface.GetProcessId(),
                ex =>
                {
                    ex.Should().BeOfType<ProxySocketConnectionFailedException>();
                    var ex2 = (ProxySocketConnectionFailedException)ex;
                    if (expectedError != null)
                        ex2.SocketError.Should().Be(expectedError);
                    extraValidation?.Invoke(ex2);
                });
        }
    }
}
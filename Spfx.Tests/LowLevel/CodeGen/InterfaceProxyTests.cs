using System;
using Spfx.Utilities.Threading;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Spfx.Reflection;
using Spfx.Runtime.Client;
using Spfx.Runtime.Client.Events;
using Spfx.Runtime.Messages;

namespace Spfx.Tests.LowLevel.CodeGen
{
    [TestFixture, Parallelizable]
    public class InterfaceProxyTests : CommonTestClass
    {
        public interface ITestInterface1
        {
            Task Test();
        }
        public interface ITestInterface2
        {
            Task<int> Test();
        }
        public interface ITestInterface3
        {
            Task Test(long inParam);
        }
        public interface ITestInterface4
        {
            Task Test(in long inParam);
        }

        public class TestConnection : IClientInterprocessConnection
        {
            private readonly Func<IInterprocessMessage, CancellationToken, Task<object>> m_messageHandler;

            public TestConnection(Func<IInterprocessMessage, CancellationToken, Task<object>> messageHandler)
            {
                m_messageHandler = messageHandler;
            }

            public ProcessEndpointAddress Destination => throw new NotImplementedException();

            public event EventHandler ConnectionLost { add { } remove { } }

            public ValueTask ChangeEventSubscription(EventSubscriptionChangeRequest req)
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public ValueTask<ProcessEndpointMethodDescriptor> GetRemoteMethodDescriptor(ProcessEndpointAddress destination, ReflectedMethodInfo calledMethod)
            {
                throw new NotImplementedException();
            }

            public void Initialize()
            {
                throw new NotImplementedException();
            }

            public Task<object> SerializeAndSendMessage(IInterprocessMessage req, CancellationToken ct = default)
            {
                return m_messageHandler(req, ct);
            }

            public async Task<T> SerializeAndSendMessage<T>(IInterprocessMessage req, CancellationToken ct = default)
            {
                var raw = await SerializeAndSendMessage(req, ct);
                return (T)raw;
            }

            public ValueTask SubscribeEndpointLost(ProcessEndpointAddress address, Action<EndpointLostEventArgs, object> handler, object state)
            {
                throw new NotImplementedException();
            }

            public void UnsubscribeEndpointLost(ProcessEndpointAddress address, Action<EndpointLostEventArgs, object> handler, object state)
            {
                throw new NotImplementedException();
            }
        }

        [Test/*, Parallelizable*/]
        public void Test_ProxyIntercept_NoArgs_VoidReturn_Success()
        {
            var rawInterface = ProcessProxyFactory.CreateImplementation<ITestInterface1>();

            const object expectedResult = null;

            ProcessEndpointAddress actualAddress = null;
            var handler = new TestConnection((req, ct) =>
            {
                actualAddress = req.Destination;
                AssertArgsEqual(req, null);
                return Task.FromResult(expectedResult);
            });

            var expectedAddress = ProcessEndpointAddress.Create("localhost", "test1", "test2");
            rawInterface.Initialize(new ProcessProxy(), handler, expectedAddress);
            var iface = (ITestInterface1)rawInterface;

            Unwrap(iface.Test());

            Assert.AreEqual(expectedAddress, actualAddress);
        }

        [Test/*, Parallelizable*/]
        public void Test_ProxyIntercept_1ArgLong_VoidReturn_Success()
        {
            var rawInterface = ProcessProxyFactory.CreateImplementation<ITestInterface3>();

            const object expectedResult = null;

            long longval = 191919191919;
            var expectedArgs = new object[] { longval };

            ProcessEndpointAddress actualAddress = null;
            var handler = new TestConnection((req, ct) =>
            {
                actualAddress = req.Destination;
                AssertArgsEqual(req, expectedArgs);
                return Task.FromResult(expectedResult);
            });

            var expectedAddress = ProcessEndpointAddress.Create("localhost", "test1", "test2");
            rawInterface.Initialize(new ProcessProxy(), handler, expectedAddress);
            var iface = (ITestInterface3)rawInterface;

            Unwrap(iface.Test(longval));

            Assert.AreEqual(expectedAddress, actualAddress);
        }

        [Test/*, Parallelizable*/]
        public void Test_ProxyIntercept_1ArgRefInLong_VoidReturn_Success()
        {
            var rawInterface = ProcessProxyFactory.CreateImplementation<ITestInterface4>();

            const object expectedResult = null;

            long longval = 191919191919;
            var expectedArgs = new object[] { longval };

            ProcessEndpointAddress actualAddress = null;
            var handler = new TestConnection((req, ct) =>
            {
                actualAddress = req.Destination;
                AssertArgsEqual(req, expectedArgs);
                return Task.FromResult(expectedResult);
            });

            var expectedAddress = ProcessEndpointAddress.Create("localhost", "test1", "test2");
            rawInterface.Initialize(new ProcessProxy(), handler, expectedAddress);
            var iface = (ITestInterface4)rawInterface;

            Unwrap(iface.Test(longval));

            Assert.AreEqual(expectedAddress, actualAddress);
        }

        [Test/*, Parallelizable*/]
        public void Test_ProxyIntercept_NoArgs_IntReturn_Success()
        {
            var rawInterface = ProcessProxyFactory.CreateImplementation<ITestInterface2>();

            var expectedResult = 12345;

            ProcessEndpointAddress actualAddress = null;
            var handler = new TestConnection((req, ct) =>
            {
                actualAddress = req.Destination;
                AssertArgsEqual(req, null);
                return Task.FromResult((object)expectedResult);
            });

            var expectedAddress = ProcessEndpointAddress.Create("localhost", "test1", "test2");
            rawInterface.Initialize(new ProcessProxy(), handler, expectedAddress);
            var iface = (ITestInterface2)rawInterface;

            var actualResult = Unwrap(iface.Test());

            Assert.AreEqual(expectedAddress, actualAddress);
            Assert.AreEqual(expectedResult, actualResult);
        }

        private void AssertArgsEqual(IInterprocessMessage req, object[] expectedArgs)
        {
            var actualArgs = ((RemoteCallRequest)req).Arguments;
            if (actualArgs is null)
                actualArgs = Array.Empty<object>();
            if (expectedArgs is null)
                expectedArgs = Array.Empty<object>();

            AssertRangeEqual(expectedArgs, actualArgs);
        }
    }
}

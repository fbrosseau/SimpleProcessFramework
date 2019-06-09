﻿using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Spfx.Reflection;
using Spfx.Runtime.Client;
using Spfx.Runtime.Messages;

namespace Spfx.Tests.LowLevel.CodeGen
{
    [TestFixture]
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

            public Task ChangeEventSubscription(EventRegistrationRequestInfo req)
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
        }

        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void Test_ProxyIntercept_NoArgs_VoidReturn_Success()
        {
            var rawInterface = ProcessProxyFactory.CreateImplementation<ITestInterface1>();

            object expectedResult = null;

            ProcessEndpointAddress actualAddress = null;
            var handler = new TestConnection((req, ct) =>
            {
                actualAddress = req.Destination;
                AssertArgsEqual(req, null);
                return Task.FromResult(expectedResult);
            });

            var expectedAddress = new ProcessEndpointAddress("localhost", "test1", "test2");
            rawInterface.Initialize(handler, expectedAddress);
            var iface = (ITestInterface1)rawInterface;

            TestUtilities.Unwrap(iface.Test());

            Assert.AreEqual(expectedAddress, actualAddress);
        }

        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void Test_ProxyIntercept_1ArgLong_VoidReturn_Success()
        {
            var rawInterface = ProcessProxyFactory.CreateImplementation<ITestInterface3>();

            object expectedResult = null;

            long longval = 191919191919;
            var expectedArgs = new object[] { longval };

            ProcessEndpointAddress actualAddress = null;
            var handler = new TestConnection((req, ct) =>
            {
                actualAddress = req.Destination;
                AssertArgsEqual(req, expectedArgs);
                return Task.FromResult(expectedResult);
            });

            var expectedAddress = new ProcessEndpointAddress("localhost", "test1", "test2");
            rawInterface.Initialize(handler, expectedAddress);
            var iface = (ITestInterface3)rawInterface;

            TestUtilities.Unwrap(iface.Test(longval));

            Assert.AreEqual(expectedAddress, actualAddress);
        }

        [Test, Timeout(DefaultTestTimeout), Parallelizable]
        public void Test_ProxyIntercept_1ArgRefInLong_VoidReturn_Success()
        {
            var rawInterface = ProcessProxyFactory.CreateImplementation<ITestInterface4>();

            object expectedResult = null;

            long longval = 191919191919;
            var expectedArgs = new object[] { longval };

            ProcessEndpointAddress actualAddress = null;
            var handler = new TestConnection((req, ct) =>
            {
                actualAddress = req.Destination;
                AssertArgsEqual(req, expectedArgs);
                return Task.FromResult(expectedResult);
            });

            var expectedAddress = new ProcessEndpointAddress("localhost", "test1", "test2");
            rawInterface.Initialize(handler, expectedAddress);
            var iface = (ITestInterface4)rawInterface;

            TestUtilities.Unwrap(iface.Test(longval));

            Assert.AreEqual(expectedAddress, actualAddress);
        }

        [Test, Timeout(DefaultTestTimeout), Parallelizable]
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

            var expectedAddress = new ProcessEndpointAddress("localhost", "test1", "test2");
            rawInterface.Initialize(handler, expectedAddress);
            var iface = (ITestInterface2)rawInterface;

            var actualResult = TestUtilities.Unwrap(iface.Test());

            Assert.AreEqual(expectedAddress, actualAddress);
            Assert.AreEqual(expectedResult, actualResult);
        }

        private void AssertArgsEqual(IInterprocessMessage req, object[] expectedArgs)
        {
            var actualArgs = ((RemoteCallRequest)req).GetArgsOrEmpty();

            if (expectedArgs is null)
                expectedArgs = Array.Empty<object>();

            TestUtilities.AssertRangeEqual(expectedArgs, actualArgs);
        }
    }
}

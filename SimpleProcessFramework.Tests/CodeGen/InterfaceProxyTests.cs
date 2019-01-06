using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Client;
using SimpleProcessFramework.Runtime.Messages;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Tests
{
    [TestClass]
    public class InterfaceProxyTests
    {
        public interface ITestInterface1
        {
            Task Test();
        }
        public interface ITestInterface2
        {
            Task<int> Test();
        }

        [TestMethod]
        public void Test_ProxyIntercept_NoArgs_VoidReturn_Success()
        {
            var rawInterface = ProcessProxyFactory.CreateImplementation<ITestInterface1>();

            object expectedResult = null;

            ProcessEndpointAddress actualAddress = null;
            var handler = new TestCallHandler((req, ct) =>
            {
                actualAddress = req.Destination;
                return Task.FromResult(expectedResult);
            });

            var expectedAddress = new ProcessEndpointAddress("localhost", "test1", "test2");
            rawInterface.Initialize(handler, expectedAddress);
            var iface = (ITestInterface1)rawInterface;

            Unwrap(iface.Test());

            Assert.AreEqual(expectedAddress, actualAddress);
        }

        //[TestMethod]
        public void Test_ProxyIntercept_NoArgs_IntReturn_Success()
        {
            var rawInterface = ProcessProxyFactory.CreateImplementation<ITestInterface2>();

            var expectedResult = 12345;

            ProcessEndpointAddress actualAddress = null;
            var handler = new TestCallHandler((req, ct) =>
            {
                actualAddress = req.Destination;
                return Task.FromResult((object)expectedResult);
            });

            var expectedAddress = new ProcessEndpointAddress("localhost", "test1", "test2");
            rawInterface.Initialize(handler, expectedAddress);
            var iface = (ITestInterface2)rawInterface;

            var actualResult = Unwrap(iface.Test());

            Assert.AreEqual(expectedAddress, actualAddress);
            Assert.AreEqual(expectedResult, actualResult);
        }

        private void Unwrap(Task task)
        {
            task.Wait();
        }

        private T Unwrap<T>(Task<T> task)
        {
            return task.Result;
        }
    }
}

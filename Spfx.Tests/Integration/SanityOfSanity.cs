﻿using NUnit.Framework;
using Spfx.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static Spfx.Tests.TestUtilities;

namespace Spfx.Tests.Integration
{
    public class SanityOfSanity : CommonTestClass
    {
        [DataContract]
        public class BombClassOnSerialize
        {
            [DataMember]
            public int Asdf { get => throw new TestException(); set { } }
        }

        public interface ITestInterface
        {
            Task<BombClassOnSerialize> Test();
        }

        public class TestInterface : ITestInterface
        {
            public Task<BombClassOnSerialize> Test()
            {
                return Task.FromResult(new BombClassOnSerialize());
            }
        }

        [Test, Timeout(DefaultTestTimeout)/*, Parallelizable*/]
        [Description("Ensures that remote serialization exceptions are noticed by the test")]
        public void DetectUnhandledExceptionOnSubprocessSerialize()
        {
            using (var cluster = CreateTestCluster())
            {
                var procId = "wawawa";
                var epId = "wowowo";

                var procInfo = new ProcessCreationRequest
                {
                    ProcessInfo = new ProcessCreationInfo { ProcessUniqueId = procId, ProcessKind = SimpleIsolationKind }
                };

                var epInfo = new EndpointCreationRequest
                {
                    EndpointId = epId,
                    ImplementationType = typeof(TestInterface),
                    EndpointType = typeof(ITestInterface),
                };

                Unwrap(cluster.ProcessBroker.CreateProcessAndEndpoint(procInfo, epInfo));

                var ep = CreateProxyInterface<ITestInterface>(cluster, procId, epId);
                ExpectException(ep.Test());
            }
        }
    }
}

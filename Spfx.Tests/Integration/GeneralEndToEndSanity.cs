using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Serialization;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Spfx.Tests.Integration
{
    [TestFixture(SanityTestOptions.Tcp)]
    [TestFixture(SanityTestOptions.Interprocess)]
    public partial class GeneralEndToEndSanity : CommonSpfxIntegrationTestsClass
    {
        public GeneralEndToEndSanity(SanityTestOptions options)
            : base(options)
        {
        }

        [Test/*, Parallelizable*/]
        [TestCaseSource(nameof(AllGenericSupportedFrameworks))]
        public void BasicDefaultSubprocess(TargetFramework fw)
            => CreateAndDestroySuccessfulSubprocess(p => p.TargetFramework = fw);

        [Test/*, Parallelizable*/]
        public void BasicTestInMasterProcess()
        {
            using var cluster = CreateTestCluster();
            CreateAndValidateTestInterface(cluster, cluster.MasterProcess.UniqueAddress);
        }

        [Test/*, Parallelizable*/]
        public void BasicProcessCallbackToMaster() => TestCallback(DefaultProcessKind);
        [Test/*, Parallelizable*/]
        public void FakeProcessCallbackToMaster() => TestCallback(ProcessKind.DirectlyInRootProcess);
        [Test/*, Parallelizable*/]
        public void FakeProcessCallbackToOtherProcess() => TestCallback(ProcessKind.DirectlyInRootProcess, callbackInMaster: false);
        [Test/*, Parallelizable*/]
        public void BasicProcessCallbackToOtherProcess() => TestCallback(DefaultProcessKind, callbackInMaster: false);

        [Test/*, Parallelizable*/]
        [TestCaseSource(nameof(AllNetcore_AllArchs))]
        public void BasicNetcore_SpecificRuntime(TargetFramework fw) => CreateAndDestroySuccessfulSubprocess(p => { p.TargetFramework = fw; });

        [Test/*, Parallelizable*/]
        [TestCaseSource(nameof(Simple_Netfx_And_Netcore))]
        public void BasicEnvironmentVariableSubprocess(TargetFramework fw)
        {
            var envVar = "CustomEnvVar_" + Guid.NewGuid().ToString("N");
            var envValue = "CustomEnvVar_" + Guid.NewGuid().ToString("N");

            using var cluster = CreateTestCluster();
            using var iface = CreateSuccessfulSubprocess(cluster, procInfo =>
            {
                procInfo.TargetFramework = fw;
                procInfo.ExtraEnvironmentVariables = new[] { new StringKeyValuePair(envVar, envValue) };
            });
             
            Assert.AreEqual(envValue, Unwrap(iface.TestInterface.GetEnvironmentVariable(envVar)));
        }

        private void TestCallback(ProcessKind processKind, bool callbackInMaster = true)
        {
            using var cluster = CreateTestCluster();
            TestCallback(cluster, processKind, callbackInMaster);
        }

        private void TestCallback(ProcessCluster cluster, ProcessKind processKind, bool callbackInMaster = true)
        {
            var testServices = new List<TestInterfaceWrapper>();
            var test = CreateSuccessfulSubprocess(cluster, proc => proc.TargetFramework = TargetFramework.Create(processKind));

            testServices.Add(test);

            string targetProcess = WellKnownEndpoints.MasterProcessUniqueId;
            if (!callbackInMaster)
            {
                var test2 = CreateSuccessfulSubprocess(cluster, proc => proc.TargetFramework = TargetFramework.Create(processKind));
                testServices.Add(test2);
                targetProcess = ProcessProxy.GetEndpointAddress(test2.TestInterface).TargetProcess;
            }

            var proxy = CreateProxy(cluster);
            var targetEndpointBroker = CreateProxyInterface<IEndpointBroker>(proxy, cluster, targetProcess, WellKnownEndpoints.EndpointBroker);

            var callbackEndpoint = "Cb_" + GetNextUniqueId();

            Unwrap(targetEndpointBroker.CreateEndpoint(new EndpointCreationRequest
            {
                EndpointId = callbackEndpoint,
                EndpointType = typeof(ICallbackInterface),
                ImplementationType = typeof(CallbackInterface),
                Options = ProcessCreationOptions.ThrowIfExists
            }));

            var input = 5;
            var res = Unwrap(test.TestInterface.Callback($"/{targetProcess}/{callbackEndpoint}", input));
            Assert.AreEqual(input * 2, res);

            foreach (var svc in testServices)
            {
                svc.Dispose();
            }
        }

        private void CreateAndDestroySuccessfulSubprocess(Action<ProcessCreationInfo> requestCustomization = null, Action<ProcessClusterConfiguration> customConfig = null)
        {
            using var cluster = CreateTestCluster(customConfig);
            CreateSuccessfulSubprocess(cluster, requestCustomization).Dispose();
        }
    }
}
using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Utilities.Runtime;
using System;
using System.Collections.Generic;

namespace Spfx.Tests.Integration
{
    [TestFixture(SanityTestOptions.Tcp)]
    [TestFixture(SanityTestOptions.Interprocess)]
    [Parallelizable(ParallelScope.Children)]
    public partial class GeneralEndToEndSanity : CommonSpfxIntegrationTestsClass
    {
        public GeneralEndToEndSanity(SanityTestOptions options)
            : base(options)
        {
        }

        [Test]
        [TestCaseSource(nameof(AllGenericSupportedFrameworks))]
        public void BasicDefaultSubprocess(TargetFramework fw)
            => CreateAndDestroySuccessfulSubprocess(p => p.TargetFramework = fw);

        [Test]
        public void BasicTestInMasterProcess()
        {
            using var cluster = CreateTestCluster();
            CreateAndValidateTestInterface(cluster, cluster.MasterProcess.UniqueAddress);
        }

        [Test]
        public void BasicProcessCallbackToMaster() => TestCallback(DefaultProcessKind);
        [Test]
        public void FakeProcessCallbackToMaster() => TestCallback(ProcessKind.DirectlyInRootProcess);
        [Test]
        public void FakeProcessCallbackToOtherProcess() => TestCallback(ProcessKind.DirectlyInRootProcess, callbackInMaster: false);
        [Test]
        public void BasicProcessCallbackToOtherProcess() => TestCallback(DefaultProcessKind, callbackInMaster: false);

        [Test]
        [TestCaseSource(nameof(AllNetcore_AllArchs))]
        public void BasicNetcore_SpecificRuntime(TargetFramework fw) => CreateAndDestroySuccessfulSubprocess(p => { p.TargetFramework = fw; });

        [Test]
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

        private void TestCallback(ProcessKind processKind, bool callbackInMaster = true, ClusterCustomizationDelegate clusterCustomization = null)
        {
            using var cluster = CreateTestCluster(clusterCustomization);
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
    }
}
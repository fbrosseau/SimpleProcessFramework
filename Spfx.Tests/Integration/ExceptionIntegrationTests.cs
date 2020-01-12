using NUnit.Framework;
using Spfx.Interfaces;
using Spfx.Serialization;
using System;

namespace Spfx.Tests.Integration
{
    [TestFixture(SanityTestOptions.Tcp)]
    [TestFixture(SanityTestOptions.Interprocess)]
    public class ExceptionIntegrationTests : CommonSpfxIntegrationTestsClass
    {
        public ExceptionIntegrationTests(SanityTestOptions op)
            : base(op)
        {
        }

        [Test/*, Parallelizable*/]
        public void ThrowCustomException_NotSerializable()
        {
            using var cluster = CreateTestCluster();
            using var svc = CreateSuccessfulSubprocess(cluster, p => p.TargetFramework = TargetFramework.DirectlyInRootProcess);

            var text = Guid.NewGuid().ToString("N");
            ExpectException(svc.TestInterface.GetDummyValue(typeof(CustomException_NotMarshalled), exceptionText: text), typeof(RemoteException), expectedText: text, expectedStackFrame: TestInterface.ThrowingMethodName);
        }

        [Test/*, Parallelizable*/]
        public void ThrowCustomException_Serializable()
        {
            using var cluster = CreateTestCluster();
            using var svc = CreateSuccessfulSubprocess(cluster, p => p.TargetFramework = TargetFramework.DirectlyInRootProcess);

            var text = Guid.NewGuid().ToString("N");
            ExpectException<CustomException_Marshalled>(svc.TestInterface.GetDummyValue(typeof(CustomException_Marshalled), exceptionText: text),
                ex =>
                {
                    ex.AssertValues(text);
                });
        }
    }
}
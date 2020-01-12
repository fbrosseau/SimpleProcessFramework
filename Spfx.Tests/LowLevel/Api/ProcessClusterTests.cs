using NUnit.Framework;

namespace Spfx.Tests.LowLevel.Api
{
    [TestFixture, Parallelizable]
    public class ProcessClusterTests : CommonTestClass
    {
        [Test]
        public void ProcessCluster_DefaultCtor_Success()
        {
            new ProcessCluster();
        }
    }
}
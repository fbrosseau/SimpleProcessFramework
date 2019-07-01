using NUnit.Framework;

namespace Spfx.Tests.LowLevel
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
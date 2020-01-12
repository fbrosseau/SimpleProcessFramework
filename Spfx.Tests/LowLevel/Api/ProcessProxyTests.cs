using NUnit.Framework;

namespace Spfx.Tests.LowLevel.Api
{
    [TestFixture, Parallelizable]
    public class ProcessProxyTests : CommonTestClass
    {
        [Test]
        public void ProcessProxy_DefaultCtor_Success()
        {
            new ProcessProxy();
        }
    }
}
using NUnit.Framework;

namespace Spfx.Tests.Integration
{
    [Parallelizable(ParallelScope.Children)]
    public class EventRegistrationTests : CommonSpfxIntegrationTestsClass
    {
        [Test]
        public void EventRegistrationSanity()
        {
            using var svc = CreateSuccessfulSubprocess();
        }
    }
}

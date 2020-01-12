using NUnit.Framework;
using Spfx.Utilities.Runtime;
using System;

namespace Spfx.Tests.LowLevel.Api
{
    [TestFixture, Parallelizable]
    public class HostFeaturesHelperTests : CommonTestClass
    {
        [Test]
        public void HostFeaturesDescriptionTest()
        {
            Console.WriteLine(HostFeaturesHelper.DescribeHost());
        }
    }
}
using NUnit.Framework;
using Spfx.Diagnostics.Logging;
using System.IO;
using Spfx.Reflection;

namespace Spfx.Tests.Integration
{
    internal class TestStandardOutputListenerFactory : DefaultStandardOutputListenerFactory
    {
        protected override TextWriter OutputWriter { get; }
        protected override TextWriter ErrorWriter { get; }

        public TestStandardOutputListenerFactory(ITypeResolver typeResolver)
            : base(typeResolver)
        {
            OutputWriter = TestContext.Out;
            ErrorWriter = TestContext.Error;
        }
    }
}
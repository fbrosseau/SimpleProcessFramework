using NUnit.Framework;
using System;
using Spfx.Diagnostics.Logging;
using System.IO;

namespace Spfx.Tests.Integration
{
    internal class TestConsoleProvider : IConsoleProvider
    {
        public TextWriter Out { get; }
        public TextWriter Err { get; }

        public TestConsoleProvider()
        {
            Out = TestContext.Out;
            Err = TestContext.Error;
        }
    }
}
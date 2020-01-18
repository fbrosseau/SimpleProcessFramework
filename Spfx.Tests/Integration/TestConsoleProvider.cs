using NUnit.Framework;
using System;
using Spfx.Diagnostics.Logging;
using System.IO;

namespace Spfx.Tests.Integration
{
    internal class TestConsoleProvider : IConsoleProvider
    {
        [ThreadStatic]
        private static IConsoleProvider t_current;

        public TextWriter Out { get; }
        public TextWriter Err { get; }

        public static IConsoleProvider Current
        {
            get
            {
                return t_current ?? new DefaultConsoleProvider();
            }
        }

        public TestConsoleProvider()
        {
            Out = TestContext.Out;
            Err = TestContext.Error;
        }

        internal static void Setup()
        {
            t_current = new TestConsoleProvider();
        }
    }
}
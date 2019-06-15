using System;

namespace Spfx.Tests
{
    public abstract class CommonTestClass
    {
        public const int DefaultTestTimeout = TestUtilities.DefaultTestTimeout;

        protected static void Log(string msg)
        {
            Console.WriteLine(DateTime.Now + "| " + msg);
        }
    }
}

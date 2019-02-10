using System;

namespace Spfx.Tests
{
    public abstract class CommonTestClass
    {
#if DEBUG
        public const int DefaultTestTimeout = 30000;
#else
        public const int DefaultTestTimeout = 30000;
#endif

        protected static void Log(string msg)
        {
            Console.WriteLine(DateTime.Now + "| " + msg);
        }
    }
}

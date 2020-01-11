using System;

namespace Spfx.Tests.LowLevel.Threading
{
    public class CommonThreadingTestClass : CommonTestClass
    {
        protected static readonly TimeSpan OperationsTimeout = TimeSpan.FromSeconds(10);
    }
}
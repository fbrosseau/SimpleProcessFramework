using System;
using System.Diagnostics;

namespace Spfx.Utilities
{
    internal static class BadCodeAssert
    {
        [Conditional("DEBUG")]
        internal static void Assert(string message)
        {
            Debug.Fail(message);
        }

        internal static Exception ThrowInvalidOperation(string message)
        {
            Assert(message);
            throw new InvalidOperationException(message);
        }
    }
}

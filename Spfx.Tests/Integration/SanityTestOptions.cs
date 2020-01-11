using System;

namespace Spfx.Tests.Integration
{
    [Flags]
    public enum SanityTestOptions
    {
        Interprocess = 1,
        Tcp = 2
    }
}
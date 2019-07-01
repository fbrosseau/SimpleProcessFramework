using System;

namespace Spfx.Tests.Integration
{
    [Flags]
    public enum SanityTestOptions
    {
        UseIpcProxy = 1,
        UseTcpProxy = 2
    }
}
#if NETCOREAPP

using System;

namespace Spfx.Tests
{
    /// <summary>
    /// https://github.com/nunit/nunit/issues/3282
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TimeoutAttribute : Attribute
    {
        public TimeoutAttribute(int defaultTestTimeout)
        {
            _ = defaultTestTimeout;
        }
    }
}

#endif


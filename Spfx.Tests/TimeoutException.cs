using System;
using System.Collections.Generic;
using System.Text;

namespace Spfx.Tests
{
#if NETCOREAPP
    /// <summary>
    /// https://github.com/nunit/nunit/issues/3282
    /// </summary>
    public class TimeoutAttribute : Attribute
    {
        public TimeoutAttribute(int defaultTestTimeout)
        {
        }
    }
#endif
}

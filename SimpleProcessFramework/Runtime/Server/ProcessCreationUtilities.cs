using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleProcessFramework.Runtime.Server
{
    public static class ProcessCreationUtilities
    {
        public static readonly object ProcessCreationLock = new object();
    }
}

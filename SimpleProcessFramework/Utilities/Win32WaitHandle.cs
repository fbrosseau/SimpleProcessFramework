using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spfx.Utilities
{
    internal class Win32WaitHandle : WaitHandle
    {
        public Win32WaitHandle(IntPtr h)
        {
            SafeWaitHandle = new SafeWaitHandle(h, true);
        }

        public Win32WaitHandle(SafeHandle h)
        {
            SafeWaitHandle = new SafeWaitHandle(h.DangerousGetHandle(), false);
        }
    }
}

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spfx.Utilities.Interop
{
    internal class Win32ForeignProcessHandle : IDisposable
    {
        private SafeHandle m_owningProcess;
        public IntPtr Value => m_handle;
        private IntPtr m_handle;

        public Win32ForeignProcessHandle(Process owningProcess, IntPtr handle)
            : this(owningProcess.SafeHandle, handle)
        {
        }

        public Win32ForeignProcessHandle(SafeHandle owningProcess, IntPtr handle)
        {
            m_handle = handle;
            m_owningProcess = owningProcess;
        }

        public IntPtr ReleaseHandle()
        {
            return Interlocked.Exchange(ref m_handle, IntPtr.Zero);
        }

        public void Dispose()
        {
            var h = ReleaseHandle();
            if (h == IntPtr.Zero || h == (IntPtr)(-1))
                return;

            Win32Interop.CloseHandleInRemoteProcess(m_owningProcess, h);
        }
    }
}
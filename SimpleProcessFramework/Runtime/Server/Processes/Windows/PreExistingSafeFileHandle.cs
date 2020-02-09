#if WINDOWS_BUILD

using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

namespace Spfx.Runtime.Server.Processes.Windows
{
    internal class PreExistingSafeFileHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public PreExistingSafeFileHandle()
            : base(true)
        {
        }

        public void DuplicateFrom(SafeHandle h, bool inheritable, HandleAccessRights? newAccessRights = null)
        {
            Win32Interop.DuplicateLocalProcessHandle(h, inheritable, out handle, newAccessRights);
        }

        internal void AcquireHandle(ref IntPtr handleHolder)
        {
            try
            {
            }
            finally
            {
                handle = handleHolder;
                handleHolder = IntPtr.Zero;
            }
        }

        protected override bool ReleaseHandle()
        {
            return Win32Interop.CloseHandle(handle);
        }
    }
}

#endif
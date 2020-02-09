using Microsoft.Win32.SafeHandles;
using System;

namespace Spfx.Utilities
{
    internal class NoDisposeSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public NoDisposeSafeHandle()
            : base(false)
        {
            GC.SuppressFinalize(this);
        }

        public NoDisposeSafeHandle(IntPtr value)
            : this()
        {
            SetHandle(value);
        }

        protected override void Dispose(bool disposing)
        {
            return;
        }

        protected override bool ReleaseHandle()
        {
            return true;
        }
    }
}

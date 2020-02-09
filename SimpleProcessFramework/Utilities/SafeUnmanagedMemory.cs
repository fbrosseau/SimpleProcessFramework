using System;
using System.Runtime.InteropServices;

namespace Spfx.Utilities
{
    internal class UnmanagedAllocationSafeHandle : SafeHandle
    {
        public int Size { get; }

        private UnmanagedAllocationSafeHandle(IntPtr alloc, int size)
            : base(IntPtr.Zero, true)
        {
            SetHandle(alloc);
            Size = size;
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            Marshal.FreeHGlobal(handle);
            return true;
        }

        public static UnmanagedAllocationSafeHandle Alloc(int size)
        {
            IntPtr alloc = IntPtr.Zero;
            bool success = false;
            try
            {
                alloc = Marshal.AllocHGlobal(size);
                return new UnmanagedAllocationSafeHandle(alloc, size);
            }
            finally
            {
                if (!success)
                    Marshal.FreeHGlobal(alloc);
            }
        }

        public unsafe Span<T> GetSpan<T>()
        {
            return new Span<T>((void*)DangerousGetHandle(), Size);
        }
    }
}

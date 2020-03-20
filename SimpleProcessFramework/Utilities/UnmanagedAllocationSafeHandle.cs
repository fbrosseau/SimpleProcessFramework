using System;
using System.Runtime.InteropServices;

namespace Spfx.Utilities
{
    internal class UnmanagedAllocationSafeHandle : SafeHandle
    {
        public long Size { get; }
        public int Size32 => checked((int)Size);
        public override bool IsInvalid => handle == IntPtr.Zero;
        public IntPtr DangerousAllocationAddress => DangerousGetHandle();

        public UnmanagedAllocationSafeHandle(int size)
            : this((long)size)
        {
        }

        public UnmanagedAllocationSafeHandle(IntPtr size)
            : this(size.ToInt64())
        {
        }

        public UnmanagedAllocationSafeHandle(long size)
            : base(IntPtr.Zero, true)
        {
            if (size > 0)
            {
                SetHandle(Marshal.AllocHGlobal(new IntPtr(size)));
                GC.AddMemoryPressure(size);
            }
            else if (size < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            Size = size;
        }

        protected override bool ReleaseHandle()
        {
            Marshal.FreeHGlobal(handle);
            GC.RemoveMemoryPressure(Size);
            return true;
        }

        public unsafe Span<T> GetSpan<T>()
        {
            return new Span<T>((void*)DangerousGetHandle(), Size32);
        }
    }
}

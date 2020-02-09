using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Spfx.Utilities
{
    internal class DisposableGCHandle : IDisposable
    {
        private object m_boxedHandle;
        private GCHandle GCHandle => (GCHandle)m_boxedHandle;

        private static readonly object s_boxedEmptyHandle = default(GCHandle);

        public DisposableGCHandle(object target, GCHandleType type)
        {
            m_boxedHandle = GCHandle.Alloc(target, type);
        }

        public DisposableGCHandle(object target)
            : this(target, GCHandleType.Normal)
        {
        }

        public void Dispose()
        {
            var h = (GCHandle)Interlocked.Exchange(ref m_boxedHandle, s_boxedEmptyHandle);
            if (h.IsAllocated)
                h.Free();
        }

        internal IntPtr AddrOfPinnedObject()
        {
            return GCHandle.AddrOfPinnedObject();
        }
    }
}

using System;
using System.Threading;

namespace Spfx.Utilities
{
    public class DisposableAction : IDisposable
    {
        private Action m_callback;

        public DisposableAction(Action callback)
        {
            Guard.ArgumentNotNull(callback, nameof(callback));
            m_callback = callback;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref m_callback, null)?.Invoke();
        }
    }
}

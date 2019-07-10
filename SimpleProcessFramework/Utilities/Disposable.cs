using System;

namespace Spfx.Utilities
{
    public class Disposable : IDisposable
    {
        public bool HasDisposeStarted { get; private set; }
        public bool IsDisposed { get; private set; }

        protected readonly object m_disposeLock;

        protected Disposable(object disposeLockObject = null)
        {
            m_disposeLock = disposeLockObject ?? new object();
        }

        public void Dispose()
        {
            if (HasDisposeStarted)
                return;

            lock (m_disposeLock)
            {
                if (HasDisposeStarted)
                    return;
                HasDisposeStarted = true;
            }

            try
            {
                OnDispose();
            }
            finally
            {
                IsDisposed = true;
            }
        }

        protected void ThrowIfDisposed()
        {
            lock (m_disposeLock)
            {
                if (IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);
            }
        }

        protected virtual void ThrowIfDisposing()
        {
            lock (m_disposeLock)
            {
                if (HasDisposeStarted)
                    throw new ObjectDisposedException(GetType().FullName);
            }
        }

        protected virtual void OnDispose()
        {
        }
    }
}

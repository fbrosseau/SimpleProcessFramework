using System;

namespace Spfx.Utilities
{
    public class Disposable : IDisposable
    {
        public bool HasDisposeStarted { get; private set; }
        public bool IsDisposed { get; private set; }
        public virtual bool HasTeardownStarted => HasDisposeStarted;

        protected object DisposeLock { get; }

        protected Disposable(object disposeLockObject = null, bool useThisAsLock = true)
        {
            DisposeLock = disposeLockObject ?? (useThisAsLock ? this : new object());
        }

        public void Dispose()
        {
            if (HasDisposeStarted)
                return;

            lock (DisposeLock)
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
            lock (DisposeLock)
            {
                if (IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);
            }
        }

        protected virtual void ThrowIfDisposing()
        {
            lock (DisposeLock)
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

using System;
using System.Collections.Generic;

namespace Spfx.Utilities
{
    /// <summary>
    /// If <see cref="ReleaseAll"/> is not called, all the previously added items will get disposed.
    /// This object is typically itself wrapped in a using() block.
    /// </summary>
    public class DisposeBag : IDisposable
    {
        private List<IDisposable> m_disposables = new List<IDisposable>();

        public void ReleaseAll()
        {
            m_disposables = null;
        }

        public T Add<T>(T val)
            where T : IDisposable
        {
            Add((IDisposable)val);
            return val;
        }

        public void Add(Action callback)
        {
            Add(new DisposableAction(callback));
        }

        public void Add(IDisposable d)
        {
            if (m_disposables is null)
                throw new ObjectDisposedException(nameof(DisposeBag));

            m_disposables.Add(d);
        }

        public void Dispose()
        {
            if (m_disposables is null)
                return;

            foreach (var d in m_disposables)
            {
                d.Dispose();
            }

            ReleaseAll();
        }
    }
}

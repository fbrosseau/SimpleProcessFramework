using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Spfx.Utilities
{
    internal static class SimpleUniqueIdFactory
    {
        public const long InvalidId = 0;
    }

    internal class SimpleUniqueIdFactory<TValue> : Disposable
    {
        private readonly Dictionary<long, TValue> m_values = new();
        private long m_nextId;

        public long GetNextId(TValue val)
        {
            lock (m_values)
            {
                while (true)
                {
                    var id = GetRawNextId();
                    if (m_values.TryAdd(id, val))
                        return id;
                }
            }
        }

        public TValue TryGetById(long id)
        {
            lock(m_values)
            {
                m_values.TryGetValue(id, out TValue t);
                return t;
            }
        }

        public TValue RemoveById(long id)
        {
            if (id == SimpleUniqueIdFactory.InvalidId)
                return default;

            lock (m_values)
            {
                m_values.Remove(id, out TValue t);
                return t;
            }
        }

        private long GetRawNextId()
        {
            return Interlocked.Increment(ref m_nextId);
        }

        protected override void OnDispose()
        {
            base.OnDispose();
        }

        public TValue[] DisposeAndGetAllValues()
        {
            lock(m_values)
            {
                Dispose();
                var vals = m_values.Values.ToArray();
                m_values.Clear();
                return vals;
            }
        }
    }
}

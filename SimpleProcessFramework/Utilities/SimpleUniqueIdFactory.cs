using System;
using System.Collections.Generic;
using System.Threading;

namespace Spfx.Utilities
{
    internal class SimpleUniqueIdFactory
    {
        public const long InvalidId = 0;
    }

    internal class SimpleUniqueIdFactory<TValue>
    {
        private readonly Dictionary<long, TValue> m_values = new Dictionary<long, TValue>();
        private long m_nextId;

        public long GetNextId(TValue val)
        {
            try
            {
                lock (m_values)
                {
                    var id = GetRawNextId();
                    m_values.Add(id, val);
                    return id;
                }
            }
            catch (ArgumentException)
            {
                while (true)
                {
                    lock (m_values)
                    {
                        var id = GetRawNextId();
                        if (!m_values.ContainsKey(id))
                            m_values.Add(id, val);
                        return id;
                    }
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
                if(m_values.TryGetValue(id, out TValue t))
                {
                    m_values.Remove(id);
                }

                return t;
            }
        }

        private long GetRawNextId()
        {
            return Interlocked.Increment(ref m_nextId);
        }
    }
}

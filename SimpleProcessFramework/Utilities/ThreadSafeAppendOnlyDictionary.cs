using System.Collections;
using System.Collections.Generic;

namespace Spfx.Utilities
{
    internal class ThreadSafeAppendOnlyDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        private Dictionary<TKey, TValue> m_dict;
        private object m_syncRoot = new object();
        private readonly IEqualityComparer<TKey> m_comparer;

        public ThreadSafeAppendOnlyDictionary(IEqualityComparer<TKey> comparer = null)
        {
            m_comparer = comparer;
            m_dict = new Dictionary<TKey, TValue>(comparer);
        }

        public void Add(TKey key, TValue value)
        {
            this[key] = value;
        }

        public bool ContainsKey(TKey key)
        {
            return m_dict.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue val)
        {
            return m_dict.TryGetValue(key, out val);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<TKey, TValue>>)m_dict).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<TKey, TValue>>)m_dict).GetEnumerator();
        }

        public TValue this[TKey key]
        {
            get
            {
                return m_dict[key];
            }
            set
            {
                lock (m_syncRoot)
                {
                    var clone = new Dictionary<TKey, TValue>(m_dict, m_comparer);
                    clone[key] = value;
                    m_dict = clone;
                }
            }
        }
    }
}
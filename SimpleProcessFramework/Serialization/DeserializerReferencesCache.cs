using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Spfx.Serialization
{
    internal class DeserializerReferencesCache
    {
        private readonly Dictionary<int, object> m_referencesById = new Dictionary<int, object>();

        private readonly DeserializerReferencesCache m_parent;

        public static DeserializerReferencesCache HardcodedReferences { get; }

        static DeserializerReferencesCache()
        {
            HardcodedReferences = SerializerReferencesCache.HardcodedReferences.CreateReverseMap();
        }

        public DeserializerReferencesCache(DeserializerReferencesCache parent = null)
        {
            m_parent = parent;
        }

        internal object GetObject(int referenceId, bool mustExist = false)
        {
            var parentValue = m_parent?.GetObject(referenceId, mustExist: false);
            if (parentValue != null)
                return parentValue;

            if (!m_referencesById.TryGetValue(referenceId, out var val))
            {
                if (mustExist)
                    throw new SerializationException("Unknown reference in stream");
            }

            return val;
        }

        public void SetReferenceKey(object obj, int idx)
        {
            m_referencesById.Add(idx, obj);
        }
    }
}
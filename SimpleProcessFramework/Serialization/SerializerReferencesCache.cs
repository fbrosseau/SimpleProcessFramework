using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace SimpleProcessFramework.Serialization
{

    internal class SerializerReferencesCache
    {
        private readonly Dictionary<object, int> m_referencesByObject = new Dictionary<object, int>();

        private readonly SerializerReferencesCache m_parent;

        public static SerializerReferencesCache HardcodedReferences { get; }

        static SerializerReferencesCache()
        {
            HardcodedReferences = new SerializerReferencesCache();

            void AddHardcodedReference(object o)
            {
                HardcodedReferences.m_referencesByObject[o] = -HardcodedReferences.m_referencesByObject.Count - 1;
            }

            void AddHardcodedTypeReference(Type t)
            {
                AddHardcodedReference(new ReflectedTypeInfo(t));
            }

            AddHardcodedReference(new ReflectedAssemblyInfo(typeof(ReflectedAssemblyInfo).Assembly));
            
            var criticalTypes = new[] 
            {
                typeof(string),
                typeof(int),
                typeof(object),
                typeof(CancellationToken)
            };

            var criticalInternalTypes = new[]
            {
                typeof(RemoteCallRequest),
                typeof(RemoteCallCancellationRequest),
                typeof(ReflectedAssemblyInfo),
                typeof(ReflectedTypeInfo),
                typeof(ReflectedMethodInfo)
            };

            foreach (var t in criticalTypes)
            {
                AddHardcodedTypeReference(t);
            }

            var memberNames = new HashSet<string>();

            foreach (var t in criticalInternalTypes)
            {
                AddHardcodedTypeReference(t);

                var serializer = (ReflectedDataContractSerializer)DefaultBinarySerializer.GetSerializer(t);
                foreach (var member in serializer.Members)
                {
                    memberNames.Add(member.Name);
                }
            }

            foreach (var memberName in memberNames.OrderBy(s => s, StringComparer.InvariantCulture))
            {
                AddHardcodedReference(memberName);
            }
        }

        public SerializerReferencesCache(SerializerReferencesCache parent = null)
        {
            m_parent = parent;
        }

        internal int? GetCacheIndex(object obj, bool addIfMissing)
        {
            var parentIndex = m_parent?.GetCacheIndex(obj, false);
            if (parentIndex != null)
                return parentIndex;

            if (m_referencesByObject.TryGetValue(obj, out int idx))
                return idx;

            if (!addIfMissing)
                return null;

            var objectType = obj.GetType();
            var objectTypeInfo = new ReflectedTypeInfo(objectType);
            GetOrCreateCacheIndex(objectTypeInfo);

            idx = m_referencesByObject.Count;
            SetReferenceKey(obj, idx);
            return idx;
        }

        public void SetReferenceKey(object obj, int idx)
        {
            m_referencesByObject.Add(obj, idx);
        }

        internal void WriteAllReferences(SerializerSession serializerSession)
        {
            serializerSession.Writer.WriteEncodedInt32(m_referencesByObject.Count);
            foreach (var reference in m_referencesByObject.OrderBy(kvp => kvp.Value))
            {
                serializerSession.Writer.WriteEncodedInt32(reference.Value);
                DefaultBinarySerializer.Serialize(serializerSession, reference.Key, typeof(object));
            }
        }

        internal int GetOrCreateCacheIndex(object obj)
        {
            return GetCacheIndex(obj, true).Value;
        }

        internal DeserializerReferencesCache CreateReverseMap()
        {
            var output = new DeserializerReferencesCache();
            foreach(var reference in m_referencesByObject)
            {
                output.SetReferenceKey(reference.Key, reference.Value);
            }
            return output;
        }
    }
}
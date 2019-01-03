using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Client;
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

            void AddHardcodedTypeReference(Type t, bool addAllTypeVariations = false)
            {
                var typeInfo = ReflectedTypeInfo.AddWellKnownType(t);

                AddHardcodedReference(typeInfo);

                if (addAllTypeVariations)
                {
                    AddHardcodedTypeReference(t.MakeArrayType());
                    AddHardcodedTypeReference(typeof(List<>).MakeGenericType(t));
                    if (t.IsValueType)
                        AddHardcodedTypeReference(typeof(Nullable<>).MakeGenericType(t));
                }
            }

            AddHardcodedReference(ReflectedAssemblyInfo.Create(typeof(ReflectedAssemblyInfo).Assembly));
            
            var primitiveTypes = new[] 
            {
                typeof(string),
                typeof(bool),
                typeof(sbyte),
                typeof(short),
                typeof(int),
                typeof(long),
                typeof(byte),
                typeof(ushort),
                typeof(uint),
                typeof(ulong),
                typeof(Guid),
                typeof(float),
                typeof(double),
                typeof(decimal),
                typeof(object),
            };

            var criticalInternalTypes = new[]
            {
                typeof(RemoteCallRequest),
                typeof(RemoteCallCancellationRequest),
                typeof(ReflectedAssemblyInfo),
                typeof(ReflectedTypeInfo),
                typeof(ReflectedMethodInfo),
                typeof(RemoteCallFailureResponse),
                typeof(RemoteCallSuccessResponse),
                typeof(RemoteClientConnectionRequest),
                typeof(RemoteClientConnectionResponse),
            };

            AddHardcodedTypeReference(typeof(CancellationToken));

            foreach (var t in primitiveTypes)
            {
                AddHardcodedTypeReference(t, addAllTypeVariations: true);
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
            var objectTypeInfo = ReflectedTypeInfo.Create(objectType);
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
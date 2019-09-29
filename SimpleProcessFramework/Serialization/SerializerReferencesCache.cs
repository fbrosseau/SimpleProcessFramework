using Spfx.Reflection;
using Spfx.Runtime.Messages;
using Spfx.Serialization.DataContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Spfx.Serialization
{
    internal class SerializerReferencesCache
    {
        private Dictionary<object, int> m_referencesByObject;
        private KeyValuePair<object, int>[] m_orderedReferences;
        private readonly SerializerReferencesCache m_parent;

        public static SerializerReferencesCache HardcodedReferences { get; }

        static SerializerReferencesCache()
        {
            HardcodedReferences = new SerializerReferencesCache { m_referencesByObject = new Dictionary<object, int>() };

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
                typeof(char),
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
                typeof(IPAddress),
                typeof(IPEndPoint),
                typeof(DnsEndPoint),
                typeof(DateTime),
                typeof(TimeSpan),
                typeof(Version),
                typeof(X509Certificate),
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
                typeof(WrappedInterprocessMessage),
                typeof(RemoteExceptionInfo),
                typeof(MarshalledRemoteExceptionInfo)
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

            int idx = 0;
            if (m_referencesByObject?.TryGetValue(obj, out idx) == true)
                return idx;

            if (!addIfMissing)
                return null;

            var objectType = obj.GetType();
            var objectTypeInfo = ReflectedTypeInfo.Create(objectType);
            GetOrCreateCacheIndex(objectTypeInfo);

            return AddNewReference(obj);
        }

        private int AddNewReference(object obj)
        {
            if (m_referencesByObject is null)
                m_referencesByObject = new Dictionary<object, int>();

            int idx = m_referencesByObject.Count;
            m_referencesByObject.Add(obj, idx);
            m_orderedReferences = null;
            return idx;
        }

        internal void WriteAllReferences(SerializerSession serializerSession)
        {
            if (m_referencesByObject is null)
                return;

            if (m_orderedReferences is null)
            {
                var refs = new KeyValuePair<object, int>[m_referencesByObject.Count];
                ((ICollection<KeyValuePair<object, int>>)m_referencesByObject).CopyTo(refs, 0);
                Array.Sort(refs, (kvp1, kvp2) => kvp1.Value.CompareTo(kvp2.Value));
                m_orderedReferences = refs;
            }

            serializerSession.Writer.WriteEncodedUInt32(checked((uint)m_orderedReferences.Length));
            foreach (var reference in m_orderedReferences)
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
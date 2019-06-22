using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace Spfx.Serialization
{
    internal class ReflectedDataContractSerializer : ITypeSerializer
    {
        internal class ReflectedMemberTypeInfo
        {
            public Func<object, bool> IsDefaultValueForType;
            public object DefaultValueForType;
            public Type MemberType;
            public bool IsValueType;
        }

        internal class ReflectedDataMember
        {
            public string Name;
            public MemberInfo MemberInfo;
            public Func<object, object> GetAccessor;
            public Action<object, object> SetAccessor;
            public ReflectedMemberTypeInfo TypeInfo;
        }

        internal IReadOnlyList<ReflectedDataMember> Members { get; }
        private readonly Type m_reflectedType;
        private readonly bool m_isSerializedByRef;
        private readonly Func<object> m_constructor;
        private static readonly Dictionary<Type, ReflectedMemberTypeInfo> s_typeInfos = new Dictionary<Type, ReflectedMemberTypeInfo>();
        private static readonly Func<object, bool> s_fallbackIsDefaultValue = o => o is null;

        public ReflectedDataContractSerializer(Type actualType)
        {
            m_reflectedType = actualType;
            m_isSerializedByRef = actualType.GetCustomAttribute<DataContractAttribute>()?.IsReference ?? false;
            var defaultCtor = actualType.GetConstructor(Type.EmptyTypes);
            if (defaultCtor != null)
                m_constructor = () => Activator.CreateInstance(m_reflectedType);
            else
                m_constructor = () => FormatterServices.GetUninitializedObject(m_reflectedType);

            var members = new List<ReflectedDataMember>();

            foreach (var memberInfo in EnumerateAllDataMembers(actualType))
            {
                if (memberInfo.GetCustomAttribute<DataMemberAttribute>() == null)
                    continue;

                var member = new ReflectedDataMember
                {
                    Name = memberInfo.Name,
                    MemberInfo = memberInfo
                };

                Type memberType;
                if (memberInfo is FieldInfo fi)
                {
                    memberType = fi.FieldType;
                    member.GetAccessor = o => fi.GetValue(o);
                    member.SetAccessor = (o, v) => fi.SetValue(o, v);
                }
                else
                {
                    var pi = (PropertyInfo)memberInfo;
                    memberType = pi.PropertyType;

                    var getter = pi.GetGetMethod(true);
                    if (getter is null)
                        throw new InvalidOperationException($"Property {actualType.FullName}::{pi.Name} has no getter");

                    member.GetAccessor = o => getter.Invoke(o, null);

                    var setter = pi.GetSetMethod(true);
                    if (setter is null)
                    {
                        var autoBackingField = actualType.GetField($"<{pi.Name}>k__BackingField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (autoBackingField is null)
                            throw new InvalidOperationException($"Property {actualType.FullName}::{pi.Name} has no setter");

                        member.SetAccessor = (o, v) => autoBackingField.SetValue(o, v);
                    }
                    else
                    {
                        member.SetAccessor = (o, v) => setter.Invoke(o, new [] { v });
                    }
                }

                member.TypeInfo = GetTypeInfo(memberType);

                members.Add(member);
            }

            members.Sort((m1, m2) => m1.Name.CompareTo(m2.Name));
            Members = members.ToArray();
        }

        private static IEnumerable<MemberInfo> EnumerateAllDataMembers(Type actualType)
        {
            if (actualType is null || actualType == typeof(object))
                yield break;

            foreach (var m in actualType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                yield return m;

            foreach (var m in EnumerateAllDataMembers(actualType.BaseType))
                yield return m;
        }

        private ReflectedMemberTypeInfo GetTypeInfo(Type memberType)
        {
            ReflectedMemberTypeInfo info;
            lock (s_typeInfos)
            {
                if (s_typeInfos.TryGetValue(memberType, out info))
                    return info;
            }

            info = new ReflectedMemberTypeInfo
            {
                MemberType = memberType,
                IsValueType = memberType.IsValueType
            };

            if (!info.IsValueType)
            {
                info.IsDefaultValueForType = s_fallbackIsDefaultValue;
            }
            else
            {
                info.DefaultValueForType = Activator.CreateInstance(memberType);
                info.IsDefaultValueForType = o => info.DefaultValueForType.Equals(o);
            }

            lock (s_typeInfos)
            {
                if (s_typeInfos.TryGetValue(memberType, out var existing))
                    return existing;

                s_typeInfos[memberType] = info;
                return info;
            }
        }

        public void WriteObject(SerializerSession bw, object graph)
        {
            var baseLocation = bw.Stream.Position;
            bw.Stream.Position += 4;

            foreach (var mem in Members)
            {
                var value = mem.GetAccessor(graph);
                if (mem.TypeInfo.IsDefaultValueForType(value))
                    continue;

                bw.WriteReference(mem.Name);

                var locationBeforeMember = bw.Stream.Position;
                bw.Stream.Position += 4;

                if (mem.TypeInfo.IsValueType)
                    DefaultBinarySerializer.SerializeExactType(bw, value, mem.TypeInfo.MemberType);
                else
                    DefaultBinarySerializer.Serialize(bw, value, mem.TypeInfo.MemberType);

                bw.WritePositionDelta(locationBeforeMember);
            }

            bw.WritePositionDelta(baseLocation);
        }

        public object ReadObject(DeserializerSession reader)
        {
            object graph = m_constructor();

            int currentMemberIndex = 0;

            var totalGraphBytes = reader.Reader.ReadInt32();
            var graphEndPosition = reader.Stream.Position + totalGraphBytes;

            while (currentMemberIndex < Members.Count && reader.Stream.Position < graphEndPosition)
            {
                var memberName = (string)reader.ReadReference(readHeader: true);
                var memberBytes = reader.Reader.ReadInt32();

                void ReadMember()
                {
                    if (currentMemberIndex >= Members.Count)
                        return;

                    var expectedMember = Members[currentMemberIndex];
                    var comparison = StringComparer.Ordinal.Compare(memberName, expectedMember.Name);
                    if (comparison == 0)
                    {
                        object value;
                        if (expectedMember.TypeInfo.IsValueType)
                            value = DefaultBinarySerializer.DeserializeExactType(reader, expectedMember.TypeInfo.MemberType);
                        else
                            value = DefaultBinarySerializer.Deserialize(reader, expectedMember.TypeInfo.MemberType);

                        expectedMember.SetAccessor(graph, value);
                        ++currentMemberIndex;
                    }
                    else if (comparison < 0)
                    {
                        reader.Stream.Position += memberBytes;
                    }
                    else
                    {
                        expectedMember.SetAccessor(graph, expectedMember.TypeInfo.DefaultValueForType);
                        ++currentMemberIndex;
                        ReadMember();
                    }
                }

                ReadMember();
            }

            reader.Stream.Position = graphEndPosition;

            return graph;
        }
    }
}
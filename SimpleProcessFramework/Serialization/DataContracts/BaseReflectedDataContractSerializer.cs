using Spfx.Runtime.Exceptions;
using Spfx.Serialization.Serializers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace Spfx.Serialization.DataContracts
{
    internal interface ISimpleMemberInfo
    {
        public string Name { get; }
        public Type Type { get; }
    }

    internal interface IComplexObjectSerializer : ITypeSerializer
    {
        IReadOnlyList<ISimpleMemberInfo> GetSerializedMembers();
    }

    internal abstract class BaseReflectedDataContractSerializer : BaseTypeSerializer, IComplexObjectSerializer
    {
        internal class ReflectedMemberTypeInfo
        {
            public Func<object, bool> IsDefaultValueForType;
            public object DefaultValueForType;
            public Type MemberType;
            public bool IsValueType;

            public override string ToString() => MemberType.AssemblyQualifiedName;
        }

        internal class ReflectedDataMember : ISimpleMemberInfo
        {
            public string Name { get; set; }
            public Type Type { get; set; }
            public MemberInfo MemberInfo;
            public Func<object, object> GetAccessor;
            public Action<object, object> SetAccessor;
            public ReflectedMemberTypeInfo TypeInfo;

            public override string ToString()
            {
                return $"{Name} -> {TypeInfo.MemberType.Name}";
            }

            internal void SerializeMember(SerializerSession session, object graph)
            {
                var value = GetAccessor(graph);

                if (value is null)
                    return;
                else if (TypeInfo.IsDefaultValueForType?.Invoke(value) ?? false)
                    return;

                session.WriteReference(Name);

                using var sizeScope = session.CreatePositionDeltaScope();

                if (TypeInfo.IsValueType)
                    DefaultBinarySerializer.SerializeExactType(session, value, TypeInfo.MemberType);
                else
                    DefaultBinarySerializer.Serialize(session, value, TypeInfo.MemberType);
            }
        }

        internal ReflectedDataMember[] Members { get; }
        IReadOnlyList<ISimpleMemberInfo> IComplexObjectSerializer.GetSerializedMembers() => Members;

        protected readonly Type ReflectedType;
        protected readonly bool IsSerializedByRef;
        private static readonly Dictionary<Type, ReflectedMemberTypeInfo> s_typeInfos = new Dictionary<Type, ReflectedMemberTypeInfo>();

        protected BaseReflectedDataContractSerializer(Type actualType, List<ReflectedDataMember> members)
        {
            ReflectedType = actualType;
            IsSerializedByRef = actualType.GetCustomAttribute<DataContractAttribute>()?.IsReference ?? false;
            Members = members.ToArray();
        }

        private static List<ReflectedDataMember> AnalyzeMembers(Type actualType)
        {
            var members = new List<ReflectedDataMember>();

            foreach (var memberInfo in EnumerateAllDataMembers(actualType))
            {
                if (memberInfo.GetCustomAttribute<DataMemberAttribute>() == null)
                    continue;

                var member = new ReflectedDataMember
                {
                    Name = memberInfo.Name,
                    MemberInfo = memberInfo,
                };

                if (memberInfo is FieldInfo fi)
                {
                    member.Type = fi.FieldType;
                    member.GetAccessor = o => fi.GetValue(o);
                    member.SetAccessor = (o, v) => fi.SetValue(o, v);
                }
                else
                {
                    var pi = (PropertyInfo)memberInfo;
                    member.Type = pi.PropertyType;

                    var declaringType = pi.DeclaringType;

                    var getter = pi.GetGetMethod(true);
                    if (getter is null)
                        throw new BadDataContractException($"Property {declaringType.FullName}::{pi.Name} has no getter");

                    member.GetAccessor = o => getter.Invoke(o, null);

                    var setter = pi.GetSetMethod(true);
                    if (setter is null)
                    {
                        var autoBackingField = declaringType.GetField($"<{pi.Name}>k__BackingField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (autoBackingField is null)
                        {
                            if (declaringType == actualType)
                                throw new BadDataContractException($"Property {declaringType.FullName}::{pi.Name} has no setter");
                            else
                                throw new BadDataContractException($"Property {declaringType.FullName}::{pi.Name} (in {actualType.FullName}) has no setter");
                        }
                        member.SetAccessor = (o, v) => autoBackingField.SetValue(o, v);
                    }
                    else
                    {
                        member.SetAccessor = (o, v) => setter.Invoke(o, new[] { v });
                    }
                }

                member.TypeInfo = GetTypeInfo(member.Type);

                members.Add(member);
            }

            members.Sort((m1, m2) => string.CompareOrdinal(m1.Name, m2.Name));
            return members;
        }

        internal static ITypeSerializer Create(Type actualType)
        {
            var members = AnalyzeMembers(actualType);
            if (members.Count == 1)
            {
                if (SingleMemberDataContractSerializer.TryCreate(actualType, members, out var s))
                    return s;
            }

            return new ComplexReflectedDataContractSerializer(actualType, members);
        }

        private static IEnumerable<MemberInfo> EnumerateAllDataMembers(Type actualType)
        {
            if (actualType is null || actualType == typeof(object))
                yield break;

            foreach (var m in EnumerateAllDataMembers(actualType.BaseType))
                yield return m;

            foreach (var m in actualType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (m.DeclaringType == actualType)
                    yield return m;
            }
        }

        private static ReflectedMemberTypeInfo GetTypeInfo(Type memberType)
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

            if (info.IsValueType)
            {
                info.DefaultValueForType = Activator.CreateInstance(memberType);

                // Nullable<T> is actually a real null when boxed.
                // Treat that as a regular ref type.
                if (info.DefaultValueForType != null) 
                {
                    info.IsDefaultValueForType = o => info.DefaultValueForType.Equals(o);
                }
            }

            lock (s_typeInfos)
            {
                if (s_typeInfos.TryGetValue(memberType, out var existing))
                    return existing;

                s_typeInfos[memberType] = info;
                return info;
            }
        }

        public override void WriteObjectWithHeader(SerializerSession session, object graph)
        {
            if (IsSerializedByRef && session.CanAddReferences)
            {
                session.WriteReference(graph);
            }
            else
            {
                base.WriteObjectWithHeader(session, graph);
            }
        }

        public override void WriteObject(SerializerSession session, object graph)
        {
            using var graphScope = session.CreatePositionDeltaScope();

            foreach (var mem in Members)
            {
                mem.SerializeMember(session, graph);
            }
        }

        protected interface IDataContractDeserializationHandler
        {
            object GetFinalObject();
            void HandleMember(ReflectedDataMember expectedMember, object value);
            void HandleMissingMember(ReflectedDataMember expectedMember);
        }

        protected object ReadObject<THandler>(ref THandler handler, DeserializerSession reader)
            where THandler : IDataContractDeserializationHandler
        {
            int currentMemberIndex = 0;

            var totalGraphBytes = reader.Reader.ReadInt32();
            var graphEndPosition = reader.Stream.Position + totalGraphBytes;

            while (currentMemberIndex < Members.Length && reader.Stream.Position < graphEndPosition)
            {
                var memberName = (string)reader.ReadReference(readHeader: true);
                var memberBytes = reader.Reader.ReadInt32();

                void ReadMember(ref THandler h)
                {
                    if (currentMemberIndex >= Members.Length)
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

                        h.HandleMember(expectedMember, value);
                        ++currentMemberIndex;
                    }
                    else if (comparison < 0)
                    {
                        reader.Stream.Position += memberBytes;
                    }
                    else
                    {
                        h.HandleMissingMember(expectedMember);
                        ++currentMemberIndex;
                        ReadMember(ref h);
                    }
                }

                ReadMember(ref handler);
            }

            reader.Stream.Position = graphEndPosition;

            return handler.GetFinalObject();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace SimpleProcessFramework.Serialization
{
    internal class ReflectedDataContractSerializer : ITypeSerializer
    {
        private class ReflectedDataMember
        {
            public string Name;
            public MemberInfo MemberInfo;
            public Type MemberType;
            internal Func<object, object> GetAccessor;
            internal Action<object, object> SetAccessor;
        }

        private ReflectedDataMember[] m_members;
        private readonly Type m_reflectedType;
        private readonly Func<object> m_constructor;

        public ReflectedDataContractSerializer(Type actualType)
        {
            m_reflectedType = actualType;
            var defaultCtor = actualType.GetConstructor(Type.EmptyTypes);
            if (defaultCtor != null)
                m_constructor = () => Activator.CreateInstance(m_reflectedType);
            else
                m_constructor = () => FormatterServices.GetUninitializedObject(m_reflectedType);

            var members = new List<ReflectedDataMember>();

            foreach (var memberInfo in actualType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (memberInfo.GetCustomAttribute<DataMemberAttribute>() == null)
                    continue;

                var member = new ReflectedDataMember
                {
                    Name = memberInfo.Name,
                    MemberInfo = memberInfo
                };

                if(memberInfo is FieldInfo fi)
                {
                    member.MemberType = fi.FieldType;
                    member.GetAccessor = o => fi.GetValue(o);
                    member.SetAccessor = (o, v) => fi.SetValue(o, v);
                }
                else
                {
                    var pi = (PropertyInfo)memberInfo;
                    member.MemberType = pi.PropertyType;
                    member.GetAccessor = o => pi.GetValue(o);
                    member.SetAccessor = (o, v) => pi.SetValue(o, v);
                }

                members.Add(member);
            }

            members.Sort((m1, m2) => m1.Name.CompareTo(m2.Name));
            m_members = members.ToArray();
        }

        public void WriteObject(SerializerSession bw, object graph)
        {
            foreach(var mem in m_members)
            {
                var value = mem.GetAccessor(graph);
                if (value is null)
                    continue;

                bw.Writer.Write(mem.Name);
                bw.Writer.Flush();

                var location = bw.Stream.Position;
                bw.Stream.Position += 4;

                DefaultBinarySerializer.Serialize(bw, value, mem.MemberType);

                var newLocation = bw.Stream.Position;
                bw.Stream.Position = location;
                bw.Writer.Write(checked((int)(newLocation - location - 4)));
                bw.Writer.Flush();
                bw.Stream.Position = newLocation;
            }
        }

        public object ReadObject(DeserializerSession reader)
        {
            object graph = m_constructor();
            if (m_members.Length == 0)
                return graph;

            int currentMemberIndex = 0;

            while(currentMemberIndex < m_members.Length)
            {
                var memberName = reader.Reader.ReadString();
                var memberBytes = reader.Reader.ReadInt32();

                void ReadMember()
                {
                    if (currentMemberIndex >= m_members.Length)
                        return;

                    var expectedMember = m_members[currentMemberIndex];
                    var comparison = memberName.CompareTo(expectedMember.Name);
                    if (comparison == 0)
                    {
                        var value = DefaultBinarySerializer.Deserialize(reader, expectedMember.MemberType);
                        expectedMember.SetAccessor(graph, value);
                        ++currentMemberIndex;
                    }
                    else if (comparison < 0)
                    {
                        reader.Stream.Position += memberBytes;
                    }
                    else
                    {
                        ++currentMemberIndex;
                        ReadMember();
                    }
                }

                ReadMember();
            }

            return graph;
        }
    }
}
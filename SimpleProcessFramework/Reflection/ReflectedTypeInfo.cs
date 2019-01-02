using System;
using System.Linq;
using System.Runtime.Serialization;

namespace SimpleProcessFramework.Reflection
{
    [DataContract(IsReference = true)]
    public class ReflectedTypeInfo : IEquatable<ReflectedTypeInfo>
    {
        private Type m_resolvedType;

        [DataMember]
        public ReflectedAssemblyInfo Assembly { get; private set; }

        [DataMember]
        public ReflectedTypeInfo[] GenericParameters { get; private set; }

        [DataMember]
        public string Name { get; private set; }

        public Type ResolvedType
        {
            get
            {
                if (m_resolvedType is null)
                {
                    m_resolvedType = Assembly.ResolvedAssembly.GetType(Name);
                    if (m_resolvedType is null)
                        throw new TypeLoadException("Could not load type " + Name + " from assembly " + Assembly.Name);
                }

                return m_resolvedType;
            }
        }

        public ReflectedTypeInfo(Type t)
        {
            Name = t.FullName;
            Assembly = new ReflectedAssemblyInfo(t.Assembly);
            m_resolvedType = t;

            if (t.IsGenericType)
                GenericParameters = t.GetGenericArguments().Select(a => new ReflectedTypeInfo(a)).ToArray();
        }

        public override bool Equals(object obj) { return Equals(obj as ReflectedTypeInfo); }
        public override int GetHashCode() => Assembly.GetHashCode() ^ Name.GetHashCode();

        public bool Equals(ReflectedTypeInfo other)
        {
            if (other is null)
                return false;
            return other.Name == Name && other.Assembly.Equals(Assembly);
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace SimpleProcessFramework.Reflection
{
    [DataContract]
    public class ReflectedMethodInfo : IEquatable<ReflectedMethodInfo>
    {
        private MethodInfo m_resolvedMethod;

        [DataMember]
        public ReflectedTypeInfo Type { get; set; }

        [DataMember]
        public ReflectedTypeInfo[] Arguments { get; set; }

        [DataMember]
        public string Name { get; set; }

        public MethodInfo ResolvedMethod
        {
            get
            {
                if (m_resolvedMethod is null)
                {
                    m_resolvedMethod = Type.ResolvedType.GetMethod(Name);
                    if (m_resolvedMethod is null)
                        throw new MissingMethodException(Type.ResolvedType.FullName, Name);
                }

                return m_resolvedMethod;
            }
        }

        public ReflectedMethodInfo(MethodInfo m)
        {
            Name = m.Name;
            Type = m.DeclaringType;

            var args = m.GetParameters();
            if(args.Length > 0)
            {
                Arguments = args.Select(p => (ReflectedTypeInfo)p.ParameterType).ToArray();
            }

            m_resolvedMethod = m;
        }

        internal string GetUniqueName()
        {
            return $"{Name}|{GetArgumentCount()}";
        }

        public int GetArgumentCount() => Arguments?.Length ?? 0;
               
        public override bool Equals(object obj) { return Equals(obj as ReflectedMethodInfo); }
        public override int GetHashCode() => Type.GetHashCode() ^ Name.GetHashCode();
        public override string ToString() => Name;

        public bool Equals(ReflectedMethodInfo other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (other is null)
                return false;
            return other.Name == Name && other.Type.Equals(Type);
        }
    }
}

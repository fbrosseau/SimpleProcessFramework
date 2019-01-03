using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace SimpleProcessFramework.Reflection
{
    [DataContract]
    public class ReflectedMethodInfo
    {
        private MethodInfo m_resolvedMethod;

        [DataMember]
        public ReflectedTypeInfo Type { get; }

        [DataMember]
        public ReflectedTypeInfo[] Arguments { get; }

        [DataMember]
        public string Name { get; }

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
            Type = ReflectedTypeInfo.Create(m.DeclaringType);

            var args = m.GetParameters();
            if(args.Length > 0)
            {
                Arguments = args.Select(p => ReflectedTypeInfo.Create(p.ParameterType)).ToArray();
            }

            m_resolvedMethod = m;
        }
    }
}

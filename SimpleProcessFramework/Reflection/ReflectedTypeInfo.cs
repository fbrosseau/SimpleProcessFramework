using System;
using System.Linq;
using System.Runtime.Serialization;

namespace SimpleProcessFramework.Reflection
{
    public class ReflectedTypeInfo
    {
        private Type m_resolvedType;

        [DataMember]
        public ReflectedAssemblyInfo Assembly { get; }

        [DataMember]
        public ReflectedTypeInfo[] GenericParameters { get; }

        [DataMember]
        public string Name { get; }

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
    }
}

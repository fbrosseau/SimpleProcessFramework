using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

namespace SimpleProcessFramework.Reflection
{
    [DataContract]
    public class ReflectedAssemblyInfo
    {
        private Assembly m_resolvedAssembly;

        [DataMember]
        public string Name { get; }

        public Assembly ResolvedAssembly
        {
            get
            {
                if (m_resolvedAssembly is null)
                {
                    m_resolvedAssembly = Assembly.Load(Name);
                    if (m_resolvedAssembly is null)
                        throw new FileNotFoundException("Could not load assembly " + Name, Name + ".dll");
                }

                return m_resolvedAssembly;
            }
        }

        public ReflectedAssemblyInfo(Assembly assembly)
        {
            Name = Name;
            m_resolvedAssembly = assembly;
        }
    }
}

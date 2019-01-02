using Oopi.Utilities;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

namespace SimpleProcessFramework.Reflection
{
    [DataContract(IsReference = true)]
    public class ReflectedAssemblyInfo : IEquatable<ReflectedAssemblyInfo>
    {
        private Assembly m_resolvedAssembly;

        [DataMember]
        public string Name { get; private set; }

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
            Guard.ArgumentNotNull(assembly, nameof(assembly));

            Name = assembly.FullName;
            m_resolvedAssembly = assembly;
        }

        public override bool Equals(object obj) { return Equals(obj as ReflectedAssemblyInfo); }
        public override int GetHashCode() => Name.GetHashCode();

        public bool Equals(ReflectedAssemblyInfo other)
        {
            return Name == other?.Name;
        }
    }
}

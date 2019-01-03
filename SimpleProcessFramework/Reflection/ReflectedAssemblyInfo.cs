using Oopi.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

namespace SimpleProcessFramework.Reflection
{
    [DataContract(IsReference = true)]
    public class ReflectedAssemblyInfo : IEquatable<ReflectedAssemblyInfo>
    {
        private static readonly Dictionary<Assembly, ReflectedAssemblyInfo> s_knownAssemblies;

        private Assembly m_resolvedAssembly;

        static ReflectedAssemblyInfo()
        {
            var thisAsm = typeof(ReflectedAssemblyInfo).Assembly;

            s_knownAssemblies = new Dictionary<Assembly, ReflectedAssemblyInfo>
            {
                { thisAsm, new ReflectedAssemblyInfo(thisAsm) }
            };
        }

        private ReflectedAssemblyInfo(Assembly assembly)
        {
            Guard.ArgumentNotNull(assembly, nameof(assembly));

            Name = assembly.FullName;
            m_resolvedAssembly = assembly;
        }

        public static ReflectedAssemblyInfo Create(Assembly assembly)
        {
            if (s_knownAssemblies.TryGetValue(assembly, out var a))
                return a;
            return new ReflectedAssemblyInfo(assembly);
        }

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

        public override bool Equals(object obj) { return Equals(obj as ReflectedAssemblyInfo); }
        public override int GetHashCode() => Name.GetHashCode();
        public override string ToString() => Name;

        public bool Equals(ReflectedAssemblyInfo other)
        {
            return Name == other?.Name;
        }
    }
}

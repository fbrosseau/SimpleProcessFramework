using Spfx.Utilities;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

namespace Spfx.Reflection
{
    [DataContract(IsReference = true)]
    public class ReflectedAssemblyInfo : IEquatable<ReflectedAssemblyInfo>
    {
        private static readonly ThreadSafeAppendOnlyDictionary<Assembly, ReflectedAssemblyInfo> s_knownAssemblies = new ThreadSafeAppendOnlyDictionary<Assembly, ReflectedAssemblyInfo>();
        private static readonly ThreadSafeAppendOnlyDictionary<string, ReflectedAssemblyInfo> s_knownAssembliesByName = new ThreadSafeAppendOnlyDictionary<string, ReflectedAssemblyInfo>();

        private Assembly m_resolvedAssembly;

        static ReflectedAssemblyInfo()
        {
            AddWellKnownAssembly(typeof(ReflectedAssemblyInfo).Assembly);
        }

        internal static void AddWellKnownAssembly(Assembly assembly)
        {
            if (s_knownAssemblies.ContainsKey(assembly))
                return;

            var asmInfo = new ReflectedAssemblyInfo(assembly); 
            s_knownAssemblies[assembly] = asmInfo;
            s_knownAssembliesByName[assembly.FullName] = asmInfo;
        }

        private ReflectedAssemblyInfo(Assembly assembly)
            : this(assembly.FullName)
        {
            m_resolvedAssembly = assembly;
        }

        private ReflectedAssemblyInfo(string assemblyName)
        {
            Name = assemblyName;
        }

        public static ReflectedAssemblyInfo Create(Assembly assembly)
        {
            Guard.ArgumentNotNull(assembly, nameof(assembly));

            if (s_knownAssemblies.TryGetValue(assembly, out var a))
                return a;
            return new ReflectedAssemblyInfo(assembly);
        }

        [SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Serialization")]
        private static ReflectedAssemblyInfo CreateFromSerialization(string assemblyName)
        {
            if (s_knownAssembliesByName.TryGetValue(assemblyName, out var a))
                return a;

            return new ReflectedAssemblyInfo(assemblyName);
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
            if (ReferenceEquals(this, other))
                return true;
            return Name == other?.Name;
        }
    }
}

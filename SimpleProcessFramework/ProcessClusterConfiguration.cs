using System;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Server;

namespace SimpleProcessFramework
{
    public class ProcessClusterConfiguration
    {
        private ITypeResolver m_typeResolver = ProcessCluster.DefaultTypeResolver;

        public bool IsReadOnly { get; private set; }

        internal static ProcessClusterConfiguration Default { get; } = new ProcessClusterConfiguration();

        public ITypeResolver TypeResolver
        {
            get => m_typeResolver;
            set { m_typeResolver = value ?? ProcessCluster.DefaultTypeResolver; }
        }

#if WINDOWS_BUILD
        public bool UseGenericProcessSpawnOnWindows { get; set; } = true;
        public bool SupportNetfx { get; set; } = true;
        public bool Support32Bit { get; set; } = true;
        public bool SupportNetcore { get; set; } = true;
#endif

        public bool CreateExecutablesIfMissing { get; set; } = true;

#if WINDOWS_BUILD
        public string DefaultNetfxProcessName { get; set; } = "Subprocess.Netfx";
        public string DefaultNetfx32ProcessName { get; set; } = "Subprocess.Netfx32";
#endif
        public string DefaultNetcoreProcessName { get; set; } = "Subprocess.Netcore";

#if WINDOWS_BUILD
        public string DefaultNetcore32ProcessName { get; set; } = "Subprocess.Netcore";
#endif

        public ProcessClusterConfiguration Clone(bool makeReadonly = false)
        {
            if (makeReadonly && IsReadOnly)
                return this;
            return this;
        }
    }
}

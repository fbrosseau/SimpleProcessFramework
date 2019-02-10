﻿using Spfx.Interfaces;
using Spfx.Reflection;

namespace Spfx
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
        public bool SupportAppDomains { get; set; } = true;
        public bool SupportWsl { get; set; } = true;
#endif

        public bool CreateExecutablesIfMissing { get; set; } = true;

#if WINDOWS_BUILD
        public string DefaultNetfxProcessName { get; set; } = "Spfx.Process.Netfx";
        public string DefaultNetfx32ProcessName { get; set; } = "Spfx.Process.Netfx32";
#endif
        public string DefaultNetcoreProcessName { get; set; } = "Spfx.Process.Netcore";

        public ProcessKind DefaultProcessKind { get; set; } = ProcessKind.Netcore;
        public bool SupportFakeProcesses { get; set; }
        public string DefaultWslNetcoreHost { get; set; } = "dotnet";

        public ProcessClusterConfiguration Clone(bool makeReadonly = false)
        {
            if (makeReadonly && IsReadOnly)
                return this;
            return this;
        }
    }
}

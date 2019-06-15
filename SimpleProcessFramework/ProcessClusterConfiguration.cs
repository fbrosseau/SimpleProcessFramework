﻿using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Utilities;
using System.ComponentModel;

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

        public bool UseGenericProcessSpawnOnWindows { get; set; } = true;
        public bool EnableNetcore { get; set; } = true;

        public bool EnableAppDomains { get; set; } = HostFeaturesHelper.LocalMachineOsKind == OsKind.Windows;
        public bool EnableNetfx { get; set; } = HostFeaturesHelper.LocalMachineOsKind == OsKind.Windows;
        public bool Enable32Bit { get; set; } = HostFeaturesHelper.LocalMachineOsKind == OsKind.Windows;
        public bool EnableWsl { get; set; } = HostFeaturesHelper.LocalMachineOsKind == OsKind.Windows;

        public bool CreateExecutablesIfMissing { get; set; } = true;

        public string DefaultNetfxProcessName { get; set; } = "Spfx.Process.Netfx";
        public string DefaultNetfx32ProcessName { get; set; } = "Spfx.Process.Netfx32";
        public string DefaultNetcoreProcessName { get; set; } = "Spfx.Process.Netcore";

        public ProcessKind DefaultProcessKind { get; set; } = HostFeaturesHelper.LocalProcessKind;
        public bool EnableFakeProcesses { get; set; }
        public string DefaultWslNetcoreHost { get; set; } = "dotnet";

        [EditorBrowsable(EditorBrowsableState.Never)]
        public const string DefaultDefaultNetfxCodeBase = "../net472";
        public string DefaultNetfxCodeBase { get; set; } = DefaultDefaultNetfxCodeBase;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public const string DefaultDefaultNetcoreCodeBase = "../netcoreapp2.1";
        public string DefaultNetcoreCodeBase { get; set; } = DefaultDefaultNetcoreCodeBase;

        public ProcessClusterConfiguration Clone(bool makeReadonly = false)
        {
            if (makeReadonly && IsReadOnly)
                return this;
            return this;
        }
    }
}
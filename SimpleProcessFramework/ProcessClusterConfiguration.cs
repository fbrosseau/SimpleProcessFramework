using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Utilities;
using System;
using System.ComponentModel;

namespace Spfx
{
    public class ProcessClusterConfiguration
    {
        public bool IsReadOnly { get; private set; }

        internal static ProcessClusterConfiguration Default { get; } = new ProcessClusterConfiguration();

        public Type TypeResolverFactoryType { get; set; } = typeof(DefaultTypeResolverFactory);

        public bool UseGenericProcessSpawnOnWindows { get; set; } = true;
        public bool EnableNetcore { get; set; } = true;

        public bool EnableAppDomains { get; set; }
        public bool EnableNetfx { get; set; } = HostFeaturesHelper.IsNetFxSupported;
        public bool Enable32Bit { get; set; } = HostFeaturesHelper.Is32BitSupported;
        public bool EnableWsl { get; set; }

        public bool CreateExecutablesIfMissing { get; set; } = true;

        public string DefaultNetfxProcessName { get; set; } = "Spfx.Process.Netfx";
        public string DefaultNetfx32ProcessName { get; set; } = "Spfx.Process.Netfx32";
        public string DefaultNetcoreProcessName { get; set; } = "Spfx.Process.Netcore";

        [EditorBrowsable(EditorBrowsableState.Never)]
        public static readonly ProcessKind DefaultDefaultProcessKind = HostFeaturesHelper.LocalProcessKind;
        public ProcessKind DefaultProcessKind { get; set; } = DefaultDefaultProcessKind;

        public bool EnableFakeProcesses { get; set; }
        public string DefaultWslNetcoreHost { get; set; } = "dotnet";

        [EditorBrowsable(EditorBrowsableState.Never)]
        public const string DefaultDefaultNetfxCodeBase = "../net472";
        public string DefaultNetfxCodeBase { get; set; } = DefaultDefaultNetfxCodeBase;

        public string DefaultNetcoreCodeBase { get; set; } 

        public bool FallbackToBestAvailableProcessKind { get; set; }

        public TimeSpan CreateProcessTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public bool AppendProcessIdToCommandLine { get; set; } = true;

        public ProcessClusterConfiguration Clone(bool makeReadonly = false)
        {
            if (makeReadonly && IsReadOnly)
                return this;
            return this;
        }
    }
}
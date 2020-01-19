using Spfx.Diagnostics.Logging;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Utilities.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace Spfx
{
    public class ProcessClusterConfiguration
    {
        public bool IsReadOnly { get; private set; }

        internal static ProcessClusterConfiguration Default { get; }

        static ProcessClusterConfiguration()
        {
            Default = new ProcessClusterConfiguration {IsReadOnly = true};
        }

        public Type TypeResolverFactoryType { get; set; } = typeof(DefaultTypeResolverFactory);
        public IConsoleProvider ConsoleProvider { get; set; } = DefaultConsoleProvider.Instance;

        /// <summary>
        /// <para>
        /// The pattern that will be used to write console output received from the subprocesses.
        /// It is supported to include format arguments, such as {%TIME%:g} to use the 'g' format.
        /// </para>
        /// <para>
        /// Example:<br/>
        /// {%PID%}>{%MSG%}<br/>
        /// Would output: <br/>
        /// 12345>Hello World<br/>
        /// </para>
        /// <para>
        /// Supported keywords:
        /// </para>
        /// <list type="bullet">
        /// <item><description>%MSG%: The received text line</description></item>
        /// <item><description>%PID%: The subprocess' PID</description></item>
        /// <item><description>%TIME%: The current local DateTime</description></item>
        /// <item><description>%UTCTIME%: The current utc DateTime</description></item>
        /// </list>
        /// </summary>
        public string ConsoleRedirectionOutputFormat { get; set; } = "{%PID%}>{%MSG%}";

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
        public static readonly ProcessKind DefaultDefaultProcessKind = HostFeaturesHelper.LocalProcessKind.IsNetfx() ? ProcessKind.Netfx : ProcessKind.Netcore;
        public ProcessKind DefaultProcessKind { get; set; } = DefaultDefaultProcessKind;

        public bool EnableFakeProcesses { get; set; }
        public string DefaultWslNetcoreHost { get; set; } = "dotnet";

        [EditorBrowsable(EditorBrowsableState.Never)]
        public const string DefaultDefaultNetfxCodeBase = "../net48";
        public string DefaultNetfxCodeBase { get; set; } = DefaultDefaultNetfxCodeBase;

        public string DefaultNetcoreCodeBase { get; set; } 

        public bool FallbackToBestAvailableProcessKind { get; set; }

        public TimeSpan CreateProcessTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public bool AppendProcessIdToCommandLine { get; set; } = true;

        public bool EnableDebugChecks { get; set; } = HostFeaturesHelper.IsDebugBuild;

        public Dictionary<TargetFramework, string> RuntimeCodeBases { get; set; } 
            = new Dictionary<TargetFramework, string>();
        public Dictionary<TargetFramework, string> DefaultExecutableNames { get; set; }
            = new Dictionary<TargetFramework, string>();

        public string DefaultNetcoreRuntime { get; set; } = "2";
        public bool PrintErrorInRegularOutput { get; set; }

        public TimeSpan IpcConnectionKeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);
        
        public bool Append32BitSuffix { get; set; } = true;
        public string SuffixFor32BitProcesses { get; set; } = "32";

        [DataMember]
        public StringKeyValuePair[] ExtraEnvironmentVariables { get; set; }

        public ProcessClusterConfiguration Clone(bool makeReadonly = false)
        {
            if (makeReadonly && IsReadOnly)
                return this;
            return this;
        }
    }
}
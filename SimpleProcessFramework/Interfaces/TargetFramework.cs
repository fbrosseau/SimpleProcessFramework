using Spfx.Utilities.Runtime;
using System;
using System.Runtime.Serialization;

namespace Spfx.Interfaces
{
    [DataContract]
    public class TargetFramework : IEquatable<TargetFramework>
    {
        public static TargetFramework Default { get; } = new TargetFramework(ProcessKind.Default);
        public static TargetFramework CurrentFrameworkWithoutRuntime { get; } = Create(HostFeaturesHelper.LocalProcessKind);
        public static TargetFramework CurrentFramework { get; } = GetLocalFramework();
        public static TargetFramework DirectlyInRootProcess { get; } = Create(ProcessKind.DirectlyInRootProcess);

        [DataMember]
        public ProcessKind ProcessKind { get; }

        public TargetFramework(ProcessKind processKind)
        {
            ProcessKind = processKind;
        }

        public static TargetFramework Create(ProcessKind kind)
        {
            if (kind.IsNetcore())
                return NetcoreTargetFramework.Create(kind);

            switch (kind)
            {
                case ProcessKind.Default:
                    return Default;
                default:
                    return new TargetFramework(kind);
            }
        }

        public bool IsSupportedByCurrentProcess(ProcessClusterConfiguration config)
        {
            return IsSupportedByCurrentProcess(config, out _);
        }

        public virtual bool IsSupportedByCurrentProcess(ProcessClusterConfiguration config, out string reason)
        {
            return HostFeaturesHelper.IsProcessKindSupportedByCurrentProcess(ProcessKind, config, out reason);
        }

        public virtual TargetFramework GetBestAvailableFramework(ProcessClusterConfiguration config)
        {
            if (this == Default)
            {
                var configuredDefault = config.DefaultProcessKind;
                if (configuredDefault == ProcessKind.Default)
                    configuredDefault = ProcessClusterConfiguration.DefaultDefaultProcessKind;

                return Create(configuredDefault).GetBestAvailableFramework(config);
            }

            if (IsSupportedByCurrentProcess(config))
                return this;

            var kind = HostFeaturesHelper.GetBestAvailableProcessKind(ProcessKind, config);
            return Create(kind).GetBestAvailableFramework(config);
        }

        private static TargetFramework GetLocalFramework()
        {
            var kind = HostFeaturesHelper.LocalProcessKind;
            if (!kind.IsNetcore())
                return Create(kind);

            return NetcoreTargetFramework.Create(kind, NetcoreInfo.NetcoreFrameworkVersion);
        }

        public static bool operator ==(TargetFramework a, TargetFramework b) => Equals(a, b);
        public static bool operator !=(TargetFramework a, TargetFramework b) => !Equals(a, b);

        public static bool Equals(TargetFramework a, TargetFramework b)
        {
            if (a is null)
                return b is null;
            return a.Equals(b);
        }

        public virtual bool Equals(TargetFramework other)
        {
            if (other?.GetType() != GetType())
                return false;
            return ProcessKind == other.ProcessKind;
        }

        public override bool Equals(object obj) => Equals(obj as TargetFramework);
        public override int GetHashCode() => ProcessKind.GetHashCode();

        public override string ToString() => ProcessKind.ToString();
    }
}
using Spfx.Utilities;
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

            return NetcoreTargetFramework.Create(kind, NetcoreHelper.NetcoreFrameworkVersion);
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

    [DataContract]
    public class NetcoreTargetFramework : TargetFramework
    {
        public static new NetcoreTargetFramework Default { get; } = (NetcoreTargetFramework)Create("2");

        [DataMember]
        public string TargetRuntime { get; }

        private NetcoreTargetFramework(ProcessKind processKind, string targetRuntime)
            : base(processKind)
        {
            if (!ProcessKind.IsNetcore())
                throw new ArgumentException("This must be Netcore, Netcore32 or Wsl", nameof(processKind));
            TargetRuntime = targetRuntime ?? "";
        }

        public static TargetFramework Create(string targetRuntime = null)
        {
            return Create(ProcessKind.Netcore, targetRuntime);
        }

        public static NetcoreTargetFramework Create(ProcessKind kind, string targetRuntime = null)
        {
            return new NetcoreTargetFramework(kind, targetRuntime);
        }

        public override bool IsSupportedByCurrentProcess(ProcessClusterConfiguration config, out string reason)
        {
            if (!base.IsSupportedByCurrentProcess(config, out reason))
                return false;

            var runtime = TargetRuntime;
            if (string.IsNullOrWhiteSpace(runtime))
                runtime = config.DefaultNetcoreRuntime ?? "";

            var selectedVersion = NetcoreHelper.GetBestNetcoreRuntime(runtime, ProcessKind);
            if (!string.IsNullOrWhiteSpace(selectedVersion))
                return true;

            reason = "There is no installed runtime matching \"" + runtime + "\"";
            return false;
        }

        public override bool Equals(TargetFramework other) => Equals(other as NetcoreTargetFramework);

        public override string ToString()
        {
            if (string.IsNullOrEmpty(TargetRuntime))
                return base.ToString();

            return ProcessKind + " " + TargetRuntime;
        }

        public bool Equals(NetcoreTargetFramework other) => base.Equals(other) && TargetRuntime == other.TargetRuntime;
        public override int GetHashCode() => base.GetHashCode() ^ TargetRuntime.GetHashCode();
    }
}
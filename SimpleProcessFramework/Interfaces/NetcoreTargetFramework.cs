using Spfx.Utilities.Runtime;
using System;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace Spfx.Interfaces
{
    [DataContract]
    public class NetcoreTargetFramework : TargetFramework, IEquatable<NetcoreTargetFramework>
    {
        public new static NetcoreTargetFramework Default { get; } = (NetcoreTargetFramework)Create("3");
        public static NetcoreTargetFramework Default32 { get; } = Create(ProcessKind.Netcore32, Default.TargetRuntime);

        [DataMember]
        public string TargetRuntime { get; }

        public Version ParsedVersion => ParseVersion(TargetRuntime);

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

            var selectedVersion = NetcoreInfo.GetBestNetcoreRuntime(runtime, ProcessKind);
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

        public override int GetHashCode() => base.GetHashCode() ^ TargetRuntime.GetHashCode();

        public static bool operator >(NetcoreTargetFramework a, NetcoreTargetFramework b) 
            => ParseVersion(a?.TargetRuntime) > ParseVersion(b?.TargetRuntime);
        public static bool operator <(NetcoreTargetFramework a, NetcoreTargetFramework b) 
            => ParseVersion(a?.TargetRuntime) < ParseVersion(b?.TargetRuntime);
        public static bool operator >=(NetcoreTargetFramework a, NetcoreTargetFramework b)
            => ParseVersion(a?.TargetRuntime) >= ParseVersion(b?.TargetRuntime);
        public static bool operator <=(NetcoreTargetFramework a, NetcoreTargetFramework b)
            => ParseVersion(a?.TargetRuntime) <= ParseVersion(b?.TargetRuntime);
        public static bool operator ==(NetcoreTargetFramework a, NetcoreTargetFramework b)
            => Equals(a, b);
        public static bool operator !=(NetcoreTargetFramework a, NetcoreTargetFramework b)
            => !(a == b);

        public override bool Equals(object obj) 
            => Equals(obj as NetcoreTargetFramework);
        public bool Equals(NetcoreTargetFramework other)
        {
            if (other is null)
                return false;

            if (ProcessKind != other.ProcessKind)
                return false;

            return ParseVersion(TargetRuntime) == ParseVersion(other.TargetRuntime);
        }

        public static bool Equals(NetcoreTargetFramework a, NetcoreTargetFramework b)
        {
            if (a is null)
                return b is null;
            return a.Equals(b);
        }

        private static readonly Regex s_versionRegex = new Regex(@"^(?<major>\d+)(\.(?<minor>\d+))?");
        private static readonly Version s_zero = new Version(0, 0);
        private static readonly Version[][] s_wellKnownVersions =
        {
            null,
            null,
            new[]{null, new Version(2, 1), new Version(2, 2) },
            new[]{new Version(3,0), new Version(3,1) }
        };

        private static Version ParseVersion(string stringVersion)
        {
            if (stringVersion is null)
                return s_zero;

            var m = s_versionRegex.Match(stringVersion);
            if (!m.Success)
                return s_zero;

            var major = int.Parse(m.Groups["major"].Value);
            int minor = 0;
            if (m.Groups["minor"].Success)
            {
                minor = int.Parse(m.Groups["minor"].Value);
            }

            T TryGet<T>(T[] arr, int index)
            {
                if (arr?.Length > index)
                    return arr[index];
                return default;
            }

            return TryGet(TryGet(s_wellKnownVersions, major), minor)
                ?? new Version(major, minor);
        }
    }
}
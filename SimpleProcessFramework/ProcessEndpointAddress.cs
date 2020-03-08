using Spfx.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.Serialization;

namespace Spfx
{
    [DataContract]
    public sealed class ProcessEndpointAddress : IEquatable<ProcessEndpointAddress>
    {
        public static readonly StringComparer StringComparer = StringComparer.OrdinalIgnoreCase;
        public static readonly StringComparison StringComparison = StringComparison.OrdinalIgnoreCase;
        public static RelativeAddressEqualityComparer RelativeAddressComparer => RelativeAddressEqualityComparer.Instance;

        private const string RelativeAuthority = "/";

        public static ProcessEndpointAddress RelativeClusterAddress { get; } = CreateFromString(RelativeAuthority, needParse: false, checkCache: false);
        public const string Scheme = "spfx";

        private ProcessEndpointAddress m_clusterAddress;
        private ProcessEndpointAddress m_processAddress;
        private ProcessEndpointAddress m_relativeAddress;

        public ProcessEndpointAddress ClusterAddress => m_clusterAddress ?? (m_clusterAddress = CreateClusterAddress());
        public ProcessEndpointAddress ProcessAddress => m_processAddress ?? (m_processAddress = CreateProcessAddress());
        public ProcessEndpointAddress RelativeAddress => m_relativeAddress ?? (m_relativeAddress = CreateRelativeAddress());

        [DataMember]
        private string m_originalString;

        private bool m_parsed;
        private string m_hostAuthority;
        private string m_processId;
        private string m_endpointId;
        private EndPoint m_hostEndpoint;
        private int m_hashcode;

        public string HostAuthority
        {
            get
            {
                EnsureParsed();
                return m_hostAuthority;
            }
            private set => m_hostAuthority = value;
        }

        public string ProcessId
        {
            get
            {
                EnsureParsed();
                return m_processId;
            }
            private set => m_processId = value;
        }

        public string EndpointId
        {
            get
            {
                EnsureParsed();
                return m_endpointId;
            }
            private set => m_endpointId = value;
        }

        public EndPoint HostEndpoint
        {
            get
            {
                if (m_hostEndpoint is null)
                    m_hostEndpoint = EndpointHelper.ParseEndpoint(HostAuthority, ProcessCluster.DefaultRemotePort);
                return m_hostEndpoint;
            }
        }

        private ProcessEndpointAddress()
        {
        }

        public ProcessEndpointAddress Combine(string part1, string part2)
        {
            return Combine($"{part1}/{part2}");
        }

        public ProcessEndpointAddress Combine(string rightPart)
        {
            Guard.ArgumentNotNullOrEmpty(rightPart, nameof(rightPart));

            var result = m_originalString;
            if (!result.EndsWith("/"))
                result += "/";

            if (!rightPart.StartsWith("/"))
                result += rightPart;
            else
                result += rightPart.Substring(1);

            return CreateFromString(result, needParse: false, checkCache: true);
        }

        public static ProcessEndpointAddress Parse(string addr)
        {
            return CreateFromString(addr, needParse: true, checkCache: true);
        }

        public static ProcessEndpointAddress Create(string hostAuthority) => Create(hostAuthority, null);
        public static ProcessEndpointAddress Create(string hostAuthority, string targetProcess) => Create(hostAuthority, targetProcess, null);
        public static ProcessEndpointAddress Create(string hostAuthority, string targetProcess, string targetEndpoint)
        {
            var str = $"{Scheme}://{hostAuthority}/";

            if (!string.IsNullOrWhiteSpace(targetProcess))
                str += targetProcess + "/";
            if (!string.IsNullOrWhiteSpace(targetEndpoint))
                str += targetEndpoint + "/";

            return Parse(str);
        }

        [SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Serialization")]
        private static ProcessEndpointAddress CreateFromSerialization(string originalString)
        {
            return CreateFromString(originalString, needParse: false, checkCache: true);
        }
        
        private static ProcessEndpointAddress CreateFromString(string addr, bool needParse, bool checkCache)
        {
            if (checkCache)
            {
                if (addr == RelativeClusterAddress.m_originalString)
                    return RelativeClusterAddress;
                if (ProcessEndpointAddressCache.TryGetCachedValue(addr, out var ep))
                    return ep;
            }

            var a = new ProcessEndpointAddress { m_originalString = addr };
            if (needParse)
                a.EnsureParsed();
            return a;
        }

        private void EnsureParsed()
        {
            if (!TryParse())
                throw new InvalidEndpointAddressException(m_originalString);
        }

        private bool TryParse()
        {
            if (m_parsed)
                return true;

            if (!Uri.TryCreate(m_originalString, UriKind.RelativeOrAbsolute, out Uri u))
                return false;

            string[] segments;
            if (u.IsAbsoluteUri)
            {
                if (!Scheme.Equals(u.Scheme, StringComparison))
                    return false;

                m_hostAuthority = u.Authority;
                segments = u.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                m_hostAuthority = "";
                segments = m_originalString.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            }

            if (segments.Length > 0)
            {
                m_processId = segments[0];
                if (segments.Length > 1)
                    m_endpointId = segments[1];
            }

            m_parsed = true;
            return true;
        }

        public override string ToString()
        {
            return m_originalString;
        }

        public bool Equals(ProcessEndpointAddress other)
        {
            return other?.m_originalString == m_originalString;
        }

        private ProcessEndpointAddress CreateProcessAddress()
        {
            if (ProcessId is null)
                throw new InvalidOperationException("This address does not contain a TargetProcess");
            if (EndpointId is null)
                return this;
            return ClusterAddress.Combine(ProcessId);
        }

        private ProcessEndpointAddress CreateClusterAddress()
        {
            if (ProcessId is null)
                return this;
            if (string.IsNullOrWhiteSpace(HostAuthority))
                return RelativeClusterAddress;
            return Create(HostAuthority);
        }

        private ProcessEndpointAddress CreateRelativeAddress()
        {
            if (HostAuthority == RelativeAuthority)
                return this;

            return Create(RelativeAuthority, ProcessId, EndpointId);
        }
        
        public static bool Equals(ProcessEndpointAddress a, ProcessEndpointAddress b)
        {
            if (a is null)
                return b is null;
            return a.Equals(b);
        }

        public static bool operator ==(ProcessEndpointAddress a, ProcessEndpointAddress b) => Equals(a, b);
        public static bool operator !=(ProcessEndpointAddress a, ProcessEndpointAddress b) => !Equals(a, b);
        public override bool Equals(object obj) => Equals(obj as ProcessEndpointAddress);
        public override int GetHashCode() => m_hashcode != 0 ? m_hashcode : (m_hashcode = InternalGetHashCode());

        private int InternalGetHashCode()
        {
            var code = StringComparer.GetHashCode(m_originalString);
            return code == 0 ? -1 : code;
        }

        public class RelativeAddressEqualityComparer : IEqualityComparer<ProcessEndpointAddress>
        {
            public static RelativeAddressEqualityComparer Instance { get; } = new RelativeAddressEqualityComparer();

            public bool Equals(ProcessEndpointAddress x, ProcessEndpointAddress y)
            {
                return x.CreateRelativeAddress() == y.CreateRelativeAddress();
            }

            public int GetHashCode(ProcessEndpointAddress obj)
            {
                return obj.CreateRelativeAddress().GetHashCode();
            }
        }
    }
}

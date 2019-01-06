using Oopi.Utilities;
using SimpleProcessFramework.Utilities;
using System;
using System.Net;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace SimpleProcessFramework
{
    [DataContract]
    public class ProcessEndpointAddress : IEquatable<ProcessEndpointAddress>
    {
        public static readonly StringComparer StringComparer = StringComparer.OrdinalIgnoreCase;
        public static readonly StringComparison StringComparison = StringComparison.OrdinalIgnoreCase;

        public const string Scheme = "SPFW";

        [DataMember]
        private string m_originalString;

        private bool m_parsed;
        private string m_hostAuthority;
        private string m_targetProcess;
        private string m_finalEndpoint;
        private EndPoint m_hostEndpoint;

        public string HostAuthority
        {
            get
            {
                EnsureParsed();
                return m_hostAuthority;
            }
            private set
            {
                m_hostAuthority = value;
            }
        }

        public string TargetProcess
        {
            get
            {
                EnsureParsed();
                return m_targetProcess;
            }
            private set
            {
                m_targetProcess = value;
            }
        }

        public string FinalEndpoint
        {
            get
            {
                EnsureParsed();
                return m_finalEndpoint;
            }
            private set
            {
                m_finalEndpoint = value;
            }
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

        public ProcessEndpointAddress(string hostAuthority)
        {
            Guard.ArgumentNotNullOrEmpty(hostAuthority, nameof(hostAuthority));
            Initialize(hostAuthority);
        }

        public ProcessEndpointAddress(string hostAuthority, string targetProcess)
        {
            Initialize(hostAuthority, targetProcess);
        }

        public ProcessEndpointAddress(string hostAuthority, string targetProcess, string targetEndpoint)
        {
            Initialize(hostAuthority, targetProcess, targetEndpoint);
        }

        private void Initialize(string hostAuthority, string targetProcess = null, string targetEndpoint = null)
        {
            Guard.ArgumentNotNullOrEmpty(hostAuthority, nameof(hostAuthority));

            m_originalString = $"{Scheme}://{hostAuthority}/";

            if (!string.IsNullOrWhiteSpace(targetProcess))
                m_originalString += targetProcess + "/";
            if (!string.IsNullOrWhiteSpace(targetEndpoint))
                m_originalString += targetEndpoint + "/";

            m_parsed = true;
            m_hostAuthority = hostAuthority;
            m_targetProcess = targetProcess;
            m_finalEndpoint = targetEndpoint;
        }

        public static ProcessEndpointAddress Parse(string addr)
        {
            var a = new ProcessEndpointAddress();
            a.m_originalString = addr;
            a.EnsureParsed();
            return a;
        }

        private void EnsureParsed()
        {
            if (!TryParse())
                throw new InvalidOperationException("Invalid format: " + m_originalString);
        }

        private bool TryParse()
        {
            if (m_parsed)
                return true;

            if (!Uri.TryCreate(m_originalString, UriKind.Absolute, out Uri u))
                return false;

            if (!Scheme.Equals(u.Scheme, StringComparison))
                return false;

            m_parsed = true;
            m_hostAuthority = u.Authority;

            var segments = u.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0)
            {
                m_targetProcess = segments[0];
                if (segments.Length > 1)
                    m_finalEndpoint = segments[1];
            }

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

        public override bool Equals(object obj) => Equals(obj as ProcessEndpointAddress);
        public override int GetHashCode() => m_originalString.GetHashCode();
    }
}

using Oopi.Utilities;
using System;
using System.Runtime.Serialization;

namespace SimpleProcessFramework
{
    [DataContract]
    public class ProcessEndpointAddress : IEquatable<ProcessEndpointAddress>
    {
        public const string Scheme = "SPFW";

        [DataMember]
        private string m_originalString;

        private bool m_parsed;
        private string m_hostAuthority;
        private string m_targetProcess;
        private string m_targetEndpoint;

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

        public string TargetEndpoint
        {
            get
            {
                EnsureParsed();
                return m_targetEndpoint;
            }
            private set
            {
                m_targetEndpoint = value;
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
            m_targetEndpoint = targetEndpoint;
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

            if (!Scheme.Equals(u.Scheme, StringComparison.OrdinalIgnoreCase))
                return false;

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

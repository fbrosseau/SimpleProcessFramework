namespace SimpleProcessFramework
{

    public class Process
    {
        public const string MasterProcessUniqueId = "Master";

        public ProcessProxy ClusterProxy { get; }
        public string HostAuthority { get; }
        public string UniqueId { get; }
        public ProcessEndpointAddress UniqueAddress { get; }

        public Process(string hostAuthority, string uniqueId)
        {
            ClusterProxy = new ProcessProxy();
            HostAuthority = hostAuthority;
            UniqueAddress = CreateRelativeAddressInternal();
        }

        public ProcessEndpointAddress CreateRelativeAddress(string endpointAddress = null)
        {
            if (string.IsNullOrWhiteSpace(endpointAddress))
                return UniqueAddress;
            return CreateRelativeAddressInternal(endpointAddress);
        }

        private ProcessEndpointAddress CreateRelativeAddressInternal(string endpointAddress = null)
        {
            return new ProcessEndpointAddress(HostAuthority, UniqueId, endpointAddress);
        }
    }

    public class ProcessCluster
    {
        public const int DefaultRemotePort = 41412;

        private ProcessClusterConfiguration m_config;

        public ProcessProxy PrimaryProxy => MasterProcess.ClusterProxy;
        public Process MasterProcess { get; }

        public ProcessCluster()
            : this(null)
        {
        }

        public ProcessCluster(ProcessClusterConfiguration cfg)
        {
            m_config = cfg ?? ProcessClusterConfiguration.Default;
            MasterProcess = CreateMasterProcess();
        }

        private Process CreateMasterProcess()
        {
            return new Process("localhost", Process.MasterProcessUniqueId);
        }
    }
}

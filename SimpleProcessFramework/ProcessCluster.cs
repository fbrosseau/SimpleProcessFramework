using SimpleProcessFramework.Runtime.Server;
using System;
using System.Collections.Generic;

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
        private readonly HashSet<IConnectionListener> m_listeners = new HashSet<IConnectionListener>();

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

        public void AddListener(IConnectionListener listener)
        {
            lock(m_listeners)
            {
                if (!m_listeners.Add(listener))
                    throw new InvalidOperationException("This listener was added twice");
            }

            try
            {
                listener.ConnectionReceived += OnConnectionReceived;
                listener.Start();
            }
            catch
            {
                RemoveListener(listener);
            }
        }

        private void RemoveListener(IConnectionListener listener)
        {
            listener.ConnectionReceived -= OnConnectionReceived;

            lock (m_listeners)
            {
                m_listeners.Remove(listener);
            }
        }

        private void OnConnectionReceived(object sender, IpcConnectionReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}

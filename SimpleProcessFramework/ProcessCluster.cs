using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Client;
using SimpleProcessFramework.Runtime.Server;
using SimpleProcessFramework.Serialization;
using System.Collections.Generic;

namespace SimpleProcessFramework
{
    public class ProcessCluster
    {
        internal static DefaultTypeResolver DefaultTypeResolver { get; }

        static ProcessCluster()
        {
            DefaultTypeResolver = new DefaultTypeResolver();
            DefaultTypeResolver.AddService<IBinarySerializer>(new DefaultBinarySerializer());
            DefaultTypeResolver.AddService<IInternalProcessManager>(r => new ProcessManager(r));
            DefaultTypeResolver.AddService<IClientConnectionFactory>(r => new DefaultClientConnectionFactory(r.GetService<IBinarySerializer>()));
            DefaultTypeResolver.AddService<IClientConnectionManager>(r => new ClientConnectionManager(r.GetService<IInternalProcessManager>()));
        }

        public const int DefaultRemotePort = 41412;

        private ProcessClusterConfiguration m_config;
        private readonly ITypeResolver m_typeResolver;
        private readonly IInternalProcessManager m_processManager;
        private readonly IClientConnectionManager m_connectionsManager;

        public ProcessProxy PrimaryProxy => MasterProcess.ClusterProxy;
        public IProcess MasterProcess => m_processManager.MasterProcess;

        public ProcessCluster()
            : this(null)
        {
        }

        public ProcessCluster(ProcessClusterConfiguration cfg)
        {
            m_config = cfg ?? ProcessClusterConfiguration.Default;
            m_typeResolver = m_config.TypeResolver.CreateNewScope();
            m_processManager = m_typeResolver.CreateService<IInternalProcessManager>();
            m_connectionsManager = m_typeResolver.CreateService<IClientConnectionManager>();
        }

        public void AddListener(IConnectionListener listener)
        {
            m_connectionsManager.AddListener(listener);
        }

        public void RemoveListener(IConnectionListener listener)
        {
            m_connectionsManager.RemoveListener(listener);
        }
    }
}

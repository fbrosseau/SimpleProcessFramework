﻿using SimpleProcessFramework.Interfaces;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Client;
using SimpleProcessFramework.Runtime.Server;
using SimpleProcessFramework.Serialization;
using SimpleProcessFramework.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SimpleProcessFramework
{
    public class ProcessCluster
    {
        internal static DefaultTypeResolver DefaultTypeResolver { get; }

        static ProcessCluster()
        {
            DefaultTypeResolver = new DefaultTypeResolver();
            DefaultTypeResolver.RegisterSingleton<IBinarySerializer>(new DefaultBinarySerializer());
            DefaultTypeResolver.RegisterFactory<IInternalProcessBroker>(r => new ProcessBroker(r.GetSingleton<ProcessCluster>()));
            DefaultTypeResolver.RegisterFactory<IClientConnectionFactory>(r => new DefaultClientConnectionFactory(r.GetSingleton<IBinarySerializer>()));
            DefaultTypeResolver.RegisterFactory<IClientConnectionManager>(r => new ClientConnectionManager(r.GetSingleton<IInternalProcessBroker>()));
            DefaultTypeResolver.RegisterFactory<IEndpointBroker>(r => new EndpointBroker());
        }

        public const int DefaultRemotePort = 41412;

        internal ITypeResolver TypeResolver { get; }
        private ProcessClusterConfiguration m_config;
        private readonly IInternalProcessBroker m_processManager;
        private readonly IClientConnectionManager m_connectionsManager;

        public ProcessProxy PrimaryProxy => MasterProcess.ClusterProxy;
        public IProcess MasterProcess => m_processManager.MasterProcess;

        public ProcessCluster()
            : this(null)
        {
        }

        public ProcessCluster(ProcessClusterConfiguration cfg)
        {
            m_config = (cfg ?? ProcessClusterConfiguration.Default).Clone(makeReadonly: true);
            TypeResolver = m_config.TypeResolver.CreateNewScope();
            TypeResolver.RegisterSingleton(m_config);
            TypeResolver.RegisterSingleton(this);
            m_processManager = TypeResolver.CreateSingleton<IInternalProcessBroker>();
            m_connectionsManager = TypeResolver.CreateSingleton<IClientConnectionManager>();

            MasterProcess.InitializeEndpointAsync<IProcessBroker>(WellKnownEndpoints.ProcessBroker, m_processManager)
                .ExpectAlreadyCompleted();
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

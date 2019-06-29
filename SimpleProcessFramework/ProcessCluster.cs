using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Client;
using Spfx.Runtime.Server;
using Spfx.Serialization;
using Spfx.Utilities.Diagnostics;
using Spfx.Utilities.Threading;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx
{
    public class ProcessCluster : AsyncDestroyable
    {
        internal static ITypeResolver DefaultTypeResolver { get; }

        static ProcessCluster()
        {
            var resolver = new DefaultTypeResolver();
            resolver.RegisterSingleton<IBinarySerializer, DefaultBinarySerializer>();
            resolver.RegisterFactory<IInternalProcessBroker>(r => new ProcessBroker(r.GetSingleton<ProcessCluster>()));
            resolver.RegisterFactory<IClientConnectionFactory>(r => new DefaultClientConnectionFactory(r.GetSingleton<IBinarySerializer>()));
            resolver.RegisterFactory<IClientConnectionManager>(r => new ClientConnectionManager(r.GetSingleton<IIncomingClientMessagesHandler>()));
            resolver.RegisterFactory<IEndpointBroker>(r => new EndpointBroker());
            resolver.RegisterFactory<IInternalRequestsHandler>(r => new NullInternalRequestsHandler());
            resolver.RegisterFactory<ILogListener>(r => new DefaultLogListener());
            resolver.RegisterFactory<ILoggerFactory>(r => new DefaultLoggerFactory(r.CreateSingleton<ILogListener>()));
            DefaultTypeResolver = resolver;
        }

        public const int DefaultRemotePort = 41412;

        internal ITypeResolver TypeResolver { get; }
        private readonly ProcessClusterConfiguration m_config;
        private readonly IInternalProcessBroker m_processBroker;
        private readonly IClientConnectionManager m_connectionsManager;
        private readonly ILogger m_logger;

        public ProcessProxy PrimaryProxy => MasterProcess.ClusterProxy;
        public IProcess MasterProcess => m_processBroker.MasterProcess;
        public IProcessBroker ProcessBroker => m_processBroker;

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
            m_processBroker = TypeResolver.CreateSingleton<IInternalProcessBroker>();
            m_connectionsManager = TypeResolver.GetSingleton<IClientConnectionManager>();
            m_logger = TypeResolver.GetLogger(GetType(), uniqueInstance: true);

            MasterProcess.InitializeEndpointAsync<IProcessBroker>(WellKnownEndpoints.ProcessBroker, m_processBroker)
                .ExpectAlreadyCompleted();
        }

        public void AddListener(IConnectionListener listener)
        {
            m_logger.Debug?.Trace($"AddListener: {listener}");
            m_connectionsManager.AddListener(listener);
        }

        public void RemoveListener(IConnectionListener listener)
        {
            m_logger.Debug?.Trace($"RemoveListener: {listener}");
            m_connectionsManager.RemoveListener(listener);
        }

        public List<EndPoint> GetListenEndpoints()
        {
            return m_connectionsManager.GetListenEndpoints();
        }

        protected async override Task OnTeardownAsync(CancellationToken ct = default)
        {
            m_logger.Info?.Trace(nameof(OnTeardownAsync));
            await m_processBroker.TeardownAsync(ct).ConfigureAwait(false);
            await m_connectionsManager.TeardownAsync(ct).ConfigureAwait(false);
            await base.OnTeardownAsync(ct);
        }

        protected override void OnDispose()
        {
            m_logger.Info?.Trace(nameof(OnDispose));
            m_processBroker.Dispose();
            m_connectionsManager.Dispose();
            base.OnDispose();
        }
    }
}

using Spfx.Diagnostics.Logging;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Server;
using Spfx.Utilities.Threading;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx
{
    public class ProcessCluster : AsyncDestroyable
    {
        public const int DefaultRemotePort = 41412;

        internal ITypeResolver TypeResolver { get; }

        private readonly IInternalProcessBroker m_processBroker;
        private readonly IClientConnectionManager m_connectionsManager;
        private readonly ILogger m_logger;

        public ProcessClusterConfiguration Configuration { get; }
        public ProcessProxy PrimaryProxy => MasterProcess.ClusterProxy;
        public IProcess MasterProcess => m_processBroker.MasterProcess;
        public IProcessBroker ProcessBroker => m_processBroker;

        public ProcessCluster()
            : this(null)
        {
        }

        public ProcessCluster(ProcessClusterConfiguration cfg)
        {
            Configuration = (cfg ?? ProcessClusterConfiguration.Default).Clone(makeReadonly: true);
            TypeResolver = DefaultTypeResolverFactory.CreateRootTypeResolver(Configuration.TypeResolverFactoryType);
            TypeResolver.RegisterSingleton(Configuration);
            TypeResolver.RegisterSingleton(this);
            m_processBroker = TypeResolver.CreateSingleton<IInternalProcessBroker>();
            m_connectionsManager = TypeResolver.GetSingleton<IClientConnectionManager>();
            m_logger = TypeResolver.GetLogger(GetType(), uniqueInstance: true);

            TypeResolver.RegisterSingleton<IIncomingClientMessagesHandler>(
                new InternalMessageDispatcher(this));

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

        public List<EndPoint> GetConnectEndpoints()
        {
            return m_connectionsManager.GetConnectEndpoints();
        }

        protected override async ValueTask OnTeardownAsync(CancellationToken ct = default)
        {
            m_logger.Info?.Trace(nameof(OnTeardownAsync));
            await m_processBroker.TeardownAsync(ct).ConfigureAwait(false);
            await m_connectionsManager.TeardownAsync(ct).ConfigureAwait(false);
            await base.OnTeardownAsync(ct).ConfigureAwait(false);
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

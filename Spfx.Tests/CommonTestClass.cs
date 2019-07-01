using Spfx.Reflection;
using Spfx.Diagnostics.Logging;
using Spfx.Tests.Integration;
using Spfx.Runtime.Server.Listeners;
using Spfx.Utilities;
using Spfx.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using Spfx.Interfaces;

namespace Spfx.Tests
{
    public abstract class CommonTestClass
    {
        public static bool IsInMsTest { get; set; } = true;

        public static readonly ProcessKind DefaultProcessKind = ProcessClusterConfiguration.DefaultDefaultProcessKind;

        public static readonly ProcessKind SimpleIsolationKind = HostFeaturesHelper.IsAppDomainSupported
            ? ProcessKind.AppDomain : ProcessKind.Netcore;

        public const int DefaultTestTimeout = TestUtilities.DefaultTestTimeout;

        private static readonly ILogger s_logger = DefaultTypeResolverFactory.DefaultTypeResolver.CreateSingleton<ILoggerFactory>().GetLogger(typeof(CommonTestClass));
        private readonly SanityTestOptions m_options;

        protected CommonTestClass(SanityTestOptions options = SanityTestOptions.UseIpcProxy)
        {
            m_options = options;
        }

        protected ProcessCluster CreateTestCluster()
        {
            var config = new ProcessClusterConfiguration
            {
                EnableFakeProcesses = true,
                EnableAppDomains = true,
                EnableWsl = true,
                Enable32Bit = true,
                TypeResolverFactoryType = typeof(TestTypeResolverFactory)
            };

            var cluster = new ProcessCluster(config);

            if ((m_options & SanityTestOptions.UseTcpProxy) != 0)
                cluster.AddListener(new TcpInterprocessConnectionListener(0));

            var exceptionHandler = new ExceptionReportingEndpoint();
            cluster.MasterProcess.InitializeEndpointAsync<IExceptionReportingEndpoint>(ExceptionReportingEndpoint.EndpointId, exceptionHandler);

            return cluster;
        }

        private class TestTypeResolverFactory : DefaultTypeResolverFactory
        {
            public override ITypeResolver CreateRootResolver()
            {
                var typeResolver = base.CreateRootResolver();
                typeResolver.RegisterFactory<IUnhandledExceptionsHandler>(r => new TestUnhandledExceptionsHandler(r));
                return typeResolver;
            }
        }

        protected ProcessProxy CreateProxy(ProcessCluster cluster)
        {
            if ((m_options & SanityTestOptions.UseIpcProxy) != 0)
                return cluster.PrimaryProxy;
            return new ProcessProxy(encryptConnections: false);
        }

        protected T CreateProxyInterface<T>(ProcessCluster cluster, string processId, string endpointId)
        {
            return CreateProxyInterface<T>(CreateProxy(cluster), cluster, processId, endpointId);
        }

        protected T CreateProxyInterface<T>(ProcessProxy proxy, ProcessCluster cluster, string processId, string endpointId)
        {
            if ((m_options & SanityTestOptions.UseIpcProxy) != 0)
            {
                return proxy.CreateInterface<T>($"/{processId}/{endpointId}");
            }
            else
            {
                var ep = cluster.GetListenEndpoints().OfType<IPEndPoint>().First();
                if (ep.AddressFamily == AddressFamily.InterNetwork)
                {
                    if (ep.Address.Equals(IPAddress.Any))
                        ep = new IPEndPoint(IPAddress.Loopback, ep.Port);
                }
                else
                {
                    if (ep.Address.Equals(IPAddress.IPv6Any))
                        ep = new IPEndPoint(IPAddress.IPv6Loopback, ep.Port);
                }

                var addr = new ProcessEndpointAddress(ep.ToString(), processId, endpointId);
                return proxy.CreateInterface<T>(addr);
            }
        }

        protected static void Log(string msg)
        {
            s_logger.Info?.Trace(msg);
        }
    }
}

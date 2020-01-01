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
using System;
using NUnit.Framework;
using System.Collections.Generic;

namespace Spfx.Tests
{
    public abstract class CommonTestClass
    {
        public static bool IsInMsTest { get; set; } = true;
        public static readonly bool Test32Bit = HostFeaturesHelper.Is32BitSupported;

        public static readonly ProcessKind DefaultProcessKind = ProcessClusterConfiguration.DefaultDefaultProcessKind;

        public static readonly TargetFramework SimpleIsolationKind = TargetFramework.Create(
            HostFeaturesHelper.IsAppDomainSupported
            ? ProcessKind.AppDomain : ProcessKind.Netcore);

        public const int DefaultTestTimeout = TestUtilities.DefaultTestTimeout;

        public static ITypeResolver DefaultTestResolver { get; } = DefaultTypeResolverFactory.CreateRootTypeResolver<TestTypeResolverFactory>();

        private static readonly ILogger s_logger = DefaultTypeResolverFactory.DefaultTypeResolver.CreateSingleton<ILoggerFactory>().GetLogger(typeof(CommonTestClass));
        private readonly SanityTestOptions m_options;

        internal static NetcoreTargetFramework LatestNetcore = NetcoreTargetFramework.Create(ProcessKind.Netcore, "3");
        internal static NetcoreTargetFramework LatestNetcore32 = NetcoreTargetFramework.Create(ProcessKind.Netcore32, LatestNetcore.TargetRuntime);

        internal static readonly NetcoreTargetFramework[] AllNetcore = new[]
        {
            NetcoreTargetFramework.Create(ProcessKind.Netcore, "2.1"),
            NetcoreTargetFramework.Create(ProcessKind.Netcore, "2.2"),
            NetcoreTargetFramework.Create(ProcessKind.Netcore, "3.0"),
            NetcoreTargetFramework.Create(ProcessKind.Netcore, "3.1"),
        };

        internal static readonly TargetFramework[] Netfx_AllArchs = new[] { TargetFramework.Create(ProcessKind.Netfx), TargetFramework.Create(ProcessKind.Netfx32) };

        internal static readonly NetcoreTargetFramework[] AllNetcore_AllArchs
            = !Test32Bit ? AllNetcore : AllNetcore.Concat(AllNetcore.Select(n => NetcoreTargetFramework.Create(ProcessKind.Netcore32, n.TargetRuntime))).ToArray();

        internal static readonly TargetFramework[] Netfx_And_AllNetcore_AllArchs
            = Netfx_AllArchs.Concat(AllNetcore_AllArchs).ToArray();

        internal static readonly TargetFramework[] Netfx_And_NetcoreLatest_AllArchs = new[]
        {
            TargetFramework.Create(ProcessKind.Netfx),
            TargetFramework.Create(ProcessKind.Netfx32),
            LatestNetcore,
            LatestNetcore32
        };

        internal static readonly TargetFramework[] AllGenericSupportedFrameworks = GetAllGenericSupportedFrameworks();

        private static TargetFramework[] GetAllGenericSupportedFrameworks()
        {
            var result = new List<TargetFramework>();

            result.Add(NetcoreTargetFramework.Create(ProcessKind.Netcore));

            if (HostFeaturesHelper.IsNetCore32Supported)
                result.Add(NetcoreTargetFramework.Create(ProcessKind.Netcore32));

            if (HostFeaturesHelper.IsNetFxSupported)
            {
                result.Add(TargetFramework.Create(ProcessKind.Netfx));
                result.Add(TargetFramework.Create(ProcessKind.Netfx32));
            }

            if (HostFeaturesHelper.IsWslSupported)
                result.Add(TargetFramework.Create(ProcessKind.Wsl));

            return result.ToArray();
        }

        internal static readonly TargetFramework[] Netfx_And_NetcoreLatest = Netfx_And_NetcoreLatest_AllArchs.Where(f => !f.ProcessKind.Is32Bit()).ToArray();
        internal static readonly TargetFramework[] Netfx_And_Netcore3Plus_AllArchs = Netfx_AllArchs.Concat(AllNetcore_AllArchs.Where(n => n.ParsedVersion >= new Version(3, 0))).ToArray();
        internal static readonly TargetFramework[] Netfx_And_Netcore3Plus = Netfx_And_Netcore3Plus_AllArchs.Where(f => !f.ProcessKind.Is32Bit()).ToArray();

        protected CommonTestClass(SanityTestOptions options = SanityTestOptions.UseIpcProxy)
        {
            m_options = options;
        }

        protected ProcessCluster CreateTestCluster(Action<ProcessClusterConfiguration> customConfig = null)
        {
            var config = new ProcessClusterConfiguration
            {
                EnableFakeProcesses = true,
                EnableAppDomains = true,
                EnableWsl = true,
                Enable32Bit = true,
                TypeResolverFactoryType = typeof(TestTypeResolverFactory)
            };

            customConfig?.Invoke(config);

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
                typeResolver.RegisterFactory<ILoggerFactory>(r => new DefaultLoggerFactory(r));
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

        public enum ThrowAction
        {
            NoThrow,
            Throw
        }

        internal static void MaybeAssertThrows<TEx>(ThrowAction expectThrow, Action callback, Action<TEx> exceptionCallback)
            where TEx : Exception
        {
            if (expectThrow == ThrowAction.NoThrow)
                callback();
            else
                AssertThrows(callback, exceptionCallback);
        }

        internal static void AssertThrows<TEx>(Action callback, Action<TEx> exceptionCallback)
            where TEx : Exception
        {
            AssertThrows(callback, (Exception ex) =>
            {
                Assert.IsAssignableFrom(typeof(TEx), ex);
                exceptionCallback((TEx)ex);
            });
        }

        internal static void AssertThrows(Action callback, Action<Exception> exceptionCallback = null)
        {
            Exception caughtEx = null;
            try
            {
                callback();
            }
            catch (Exception ex)
            {
                caughtEx = ex;
            }

            Assert.IsNotNull(caughtEx, "The callback did not throw");
            s_logger.Debug?.Trace("Caught " + caughtEx.GetType().FullName);
            exceptionCallback?.Invoke(caughtEx);
        }
    }
}

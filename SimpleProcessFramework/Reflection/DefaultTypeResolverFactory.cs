﻿using Spfx.Diagnostics;
using Spfx.Diagnostics.Logging;
using Spfx.Interfaces;
using Spfx.Runtime.Client;
using Spfx.Runtime.Server;
using Spfx.Runtime.Server.Listeners;
using Spfx.Serialization;
using System;

namespace Spfx.Reflection
{
    public class DefaultTypeResolverFactory : ITypeResolverFactory
    {
        private static readonly DefaultTypeResolverFactory s_instance = new DefaultTypeResolverFactory();
        internal static ITypeResolver DefaultTypeResolver { get; }

        static DefaultTypeResolverFactory()
        {
            var resolver = new DefaultTypeResolver();
            resolver.RegisterFactory<IUnhandledExceptionsHandler>(r => new DefaultUnhandledExceptionHandler(r));
            resolver.RegisterFactory<IConsoleProvider>(r => new DefaultConsoleProvider());
            resolver.RegisterFactory<ILogListener>(r => new ConsoleOutputLogListener(r));
            resolver.RegisterFactory<ILoggerFactory>(r => NullLoggerFactory.Instance);
            resolver.RegisterFactory<IBinarySerializer>(r => new DefaultBinarySerializer(r));
            resolver.RegisterFactory<IInternalProcessBroker>(r => new ProcessBroker(r.GetSingleton<ProcessCluster>()));
            resolver.RegisterFactory<IClientConnectionFactory>(r => new TcpTlsClientConnectionFactory(r));
            resolver.RegisterFactory<IClientConnectionManager>(r => new ClientConnectionManager(r));
            resolver.RegisterFactory<IEndpointBroker>(r => new EndpointBroker());
            resolver.RegisterFactory<IInternalRequestsHandler>(r => new NullInternalRequestsHandler());
            resolver.RegisterFactory<ILocalConnectionFactory>(r => new NullLocalConnectionFactory());
            resolver.RegisterFactory<IStandardOutputListenerFactory>(r => new DefaultStandardOutputListenerFactory(r));
            resolver.RegisterFactory(r => ProcessClusterConfiguration.Default);
            resolver.RegisterFactory(r => new SubProcessConfiguration());
            resolver.RegisterFactory<IFailureCallResponsesFactory>(r => new DefaultFailureCallResponsesFactory());
            resolver.RegisterFactory<IClientSslStreamFactory>(r => new DangerousTrustEverythingClientSslStreamFactory());
            resolver.RegisterFactory<IServerSslStreamFactory>(r => new DefaultServerSslStreamFactory());
            DefaultTypeResolver = resolver;
        }

        public static ITypeResolver CreateRootTypeResolver<TFactory>()
            where TFactory : ITypeResolverFactory
        {
            return CreateRootTypeResolver(typeof(TFactory));
        }

        public static ITypeResolver CreateRootTypeResolver(Type typeResolverFactoryType)
        {
            ITypeResolverFactory factory;
            if (typeResolverFactoryType is null || typeResolverFactoryType == typeof(DefaultTypeResolverFactory))
            {
                factory = s_instance;
            }
            else
            {
                var rawFactory = Activator.CreateInstance(typeResolverFactoryType);
                factory = rawFactory as ITypeResolverFactory;
                if (factory is null)
                    throw new ArgumentException("Type does not implement ITypeResolverFactory: " + typeResolverFactoryType.AssemblyQualifiedName, nameof(typeResolverFactoryType));
            }

            return factory.CreateRootResolver().CreateNewScope();
        }

        public virtual ITypeResolver CreateRootResolver()
        {
            return DefaultTypeResolver;
        }
    }
}
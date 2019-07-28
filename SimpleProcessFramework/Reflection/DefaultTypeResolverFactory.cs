using Spfx.Interfaces;
using Spfx.Runtime.Client;
using Spfx.Runtime.Server;
using Spfx.Serialization;
using Spfx.Diagnostics.Logging;
using System;
using Spfx.Diagnostics;

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
            resolver.RegisterFactory<ILogListener>(r => new DefaultLogListener());
            resolver.RegisterFactory<ILoggerFactory>(r => new DefaultLoggerFactory(r.CreateSingleton<ILogListener>()));
            resolver.RegisterSingleton<IBinarySerializer, DefaultBinarySerializer>();
            resolver.RegisterFactory<IInternalProcessBroker>(r => new ProcessBroker(r.GetSingleton<ProcessCluster>()));
            resolver.RegisterFactory<IClientConnectionFactory>(r => new TcpTlsClientConnectionFactory(r));
            resolver.RegisterFactory<IClientConnectionManager>(r => new ClientConnectionManager(r));
            resolver.RegisterFactory<IEndpointBroker>(r => new EndpointBroker());
            resolver.RegisterFactory<IInternalRequestsHandler>(r => new NullInternalRequestsHandler());
            resolver.RegisterFactory<ILocalConnectionFactory>(r => new NullLocalConnectionFactory());
            resolver.RegisterFactory(r => new SubProcessConfiguration());
            DefaultTypeResolver = resolver;
        }

        internal static ITypeResolver CreateRootTypeResolver(Type typeResolverFactoryType)
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
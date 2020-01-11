using Spfx.Diagnostics;
using Spfx.Diagnostics.Logging;
using Spfx.Reflection;

namespace Spfx.Tests.Integration
{
    internal class TestTypeResolverFactory : DefaultTypeResolverFactory
    {
        public override ITypeResolver CreateRootResolver()
        {
            var typeResolver = base.CreateRootResolver();
            typeResolver.RegisterFactory<IUnhandledExceptionsHandler>(r => new TestUnhandledExceptionsHandler(r));
            typeResolver.RegisterFactory<ILoggerFactory>(r => new DefaultLoggerFactory(r));
            return typeResolver;
        }
    }
}
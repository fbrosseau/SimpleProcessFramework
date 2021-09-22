using System;

namespace Spfx.Reflection
{
    internal static class DependencyInjectionFactoryProvider
    {
        private static class FactoryImpl<T, TImpl>
        {
            public static readonly Func<ITypeResolver, object> Func = CreateFactory();
        }

        internal static Func<ITypeResolver, object> GetFactory<T, TImpl>() 
            where TImpl : T
        {
            return FactoryImpl<T, TImpl>.Func;
        }

        private static Func<ITypeResolver, object> CreateFactory()
        {
            throw new NotImplementedException();
        }
    }
}

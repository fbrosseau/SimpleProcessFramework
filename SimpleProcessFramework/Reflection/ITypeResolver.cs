using System;

namespace SimpleProcessFramework.Reflection
{
    public interface ITypeResolver
    {
        T CreateService<T>(bool addResultToCache = true);
        T CreateService<T>(ITypeResolver scope, bool addResultToCache = true);
        T GetService<T>();

        ITypeResolver CreateNewScope();

        void AddService<T, TImpl>()
            where TImpl : T;

        void AddService<T>(Func<ITypeResolver, T> factory);

        void AddService<T>(T service);
    }
}
﻿using System;

namespace Spfx.Reflection
{
    public interface ITypeResolver
    {
        T CreateSingleton<T>(bool addResultToCache = true);
        T CreateSingleton<T>(ITypeResolver scope, bool addResultToCache = true);
        T GetSingleton<T>(bool ignoreErrors = false);

        ITypeResolver CreateNewScope();

        void RegisterFactory<T, TImpl>()
            where TImpl : T;

        void RegisterFactory<T>(Func<ITypeResolver, T> factory);

        void RegisterSingleton<T>(T service);

        object CreateInstance(Type interfaceType, Type implementationType);
    }
}
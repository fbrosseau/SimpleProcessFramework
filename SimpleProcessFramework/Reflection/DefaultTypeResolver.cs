using Spfx.Utilities;
using System;
using System.Collections.Generic;

namespace Spfx.Reflection
{
    internal class DefaultTypeResolver : ITypeResolver
    {
        private readonly Dictionary<Type, Func<ITypeResolver, object>> m_factories = new Dictionary<Type, Func<ITypeResolver, object>>();
        private readonly Dictionary<Type, object> m_services = new Dictionary<Type, object>();
        private readonly ITypeResolver m_parent;

        public void RegisterFactory<T, TImpl>()
            where TImpl : T
        {
            Func<ITypeResolver, object> realFactory = DependencyInjectionFactoryProvider.GetFactory<T, TImpl>();
            RegisterFactory(ReflectionUtilities.GetType<T>(), realFactory);
        }

        public void RegisterFactory<T>(Func<ITypeResolver, T> factory)
        {
            RegisterFactory(ReflectionUtilities.GetType<T>(), r => factory(r));
        }

        private void RegisterFactory(Type t, Func<ITypeResolver, object> f)
        {
            lock (m_factories)
            {
                m_factories[t] = f;
            }
        }

        public DefaultTypeResolver(ITypeResolver parent = null)
        {
            m_parent = parent;
        }

        public void RegisterSingleton<T>(T service)
        {
            lock (m_services)
            {
                m_services[ReflectionUtilities.GetType<T>()] = service;
            }
        }

        public void RegisterSingleton<TInterface, TImpl>()
            where TImpl : TInterface, new()
        {
            lock (m_services)
            {
                m_services[ReflectionUtilities.GetType<TInterface>()] = new TImpl();
            }
        }

        public ITypeResolver CreateNewScope()
        {
            return new DefaultTypeResolver(this);
        }

        public T CreateSingleton<T>(bool addResultToCache)
        {
            return CreateSingleton<T>(this, addResultToCache);
        }

        public T CreateSingleton<T>(ITypeResolver scope, bool addResultToCache)
        {
            Guard.ArgumentNotNull(scope, nameof(scope));

            object s;
            lock (m_services)
            {
                m_services.TryGetValue(ReflectionUtilities.GetType<T>(), out s);
            }

            if (s != null)
                return (T)s;

            Func<ITypeResolver, object> factory;
            lock (m_factories)
            {
                m_factories.TryGetValue(ReflectionUtilities.GetType<T>(), out factory);
            }

            T service;
            if (factory is null)
            {
                if (m_parent != null)
                    service = m_parent.CreateSingleton<T>(scope, addResultToCache: false);
                else
                    throw new InvalidOperationException("Unable to build a service of type " + ReflectionUtilities.GetType<T>().FullName);
            }
            else
            {
                service = (T)factory(scope);
            }

            if (addResultToCache)
            {
                RegisterSingleton(service);
            }

            return service;
        }

        public T GetSingleton<T>()
        {
            object s;
            lock (m_services)
            {
                m_services.TryGetValue(ReflectionUtilities.GetType<T>(), out s);
            }

            if (s is null)
            {
                if (m_parent != null)
                    return m_parent.GetSingleton<T>();
                throw new InvalidOperationException("Unable to build a service of type " + ReflectionUtilities.GetType<T>().FullName);
            }

            return (T)s;
        }

        public object CreateInstance(Type interfaceType, Type implementationType)
        {
            return Activator.CreateInstance(implementationType);
        }
    }
}

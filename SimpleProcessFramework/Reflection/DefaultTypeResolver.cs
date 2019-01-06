using Oopi.Utilities;
using System;
using System.Collections.Generic;

namespace SimpleProcessFramework.Reflection
{
    internal class DefaultTypeResolver : ITypeResolver
    {
        private readonly Dictionary<Type, Func<ITypeResolver, object>> m_factories = new Dictionary<Type, Func<ITypeResolver, object>>();
        private readonly Dictionary<Type, object> m_services = new Dictionary<Type, object>();
        private readonly ITypeResolver m_parent;

        public void AddService<T, TImpl>()
            where TImpl : T
        {
            Func<ITypeResolver, object> realFactory = DependencyInjectionFactoryProvider.GetFactory<T, TImpl>();
            AddServiceFactory(ReflectionUtilities.GetType<T>(), realFactory);
        }

        public void AddService<T>(Func<ITypeResolver, T> factory)
        {
            AddServiceFactory(ReflectionUtilities.GetType<T>(), r => factory(r));
        }

        private void AddServiceFactory(Type t, Func<ITypeResolver, object> f)
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

        public void AddService<T>(T service)
        {
            lock (m_services)
            {
                m_services[ReflectionUtilities.GetType<T>()] = service;
            }
        }

        public ITypeResolver CreateNewScope()
        {
            return new DefaultTypeResolver(this);
        }

        public T CreateService<T>(bool addResultToCache)
        {
            return CreateService<T>(this, addResultToCache);
        }

        public T CreateService<T>(ITypeResolver scope, bool addResultToCache)
        {
            Guard.ArgumentNotNull(scope, nameof(scope));

            Func<ITypeResolver, object> factory;
            lock (m_factories)
            {
                m_factories.TryGetValue(ReflectionUtilities.GetType<T>(), out factory);
            }

            T service;
            if (factory is null)
            {
                if (m_parent != null)
                    service = m_parent.CreateService<T>(scope, addResultToCache: false);
                else
                    throw new InvalidOperationException("Unable to build a service of type " + ReflectionUtilities.GetType<T>().FullName);
            }
            else
            {
                service = (T)factory(scope);
            }

            if (addResultToCache)
            {
                AddService(service);
            }

            return service;
        }

        public T GetService<T>()
        {
            object s;
            lock (m_services)
            {
                m_services.TryGetValue(ReflectionUtilities.GetType<T>(), out s);
            }

            if (s is null)
            {
                if (m_parent != null)
                    return m_parent.GetService<T>();
                throw new InvalidOperationException("Unable to build a service of type " + ReflectionUtilities.GetType<T>().FullName);
            }

            return (T)s;
        }
    }
}

using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Client;
using Spfx.Runtime.Client.Events;
using Spfx.Utilities.Threading;
using System;
using System.Threading.Tasks;

namespace Spfx
{
    public interface IInternalRequestsHandler
    {
        ProcessEndpointAddress NormalizeAddress(ProcessEndpointAddress address);
    }

    internal class InternalRequestsHandler : IInternalRequestsHandler
    {
        private readonly string m_localAuthority;

        public InternalRequestsHandler(string localAuthority)
        {
            m_localAuthority = localAuthority;
        }

        public ProcessEndpointAddress NormalizeAddress(ProcessEndpointAddress address)
        {
            if (!string.IsNullOrWhiteSpace(address.HostAuthority))
                return address;
            return ProcessEndpointAddress.Create(m_localAuthority, address.ProcessId, address.EndpointId);
        }
    }

    internal class NullInternalRequestsHandler : IInternalRequestsHandler
    {
        public ProcessEndpointAddress NormalizeAddress(ProcessEndpointAddress address)
        {
            if (string.IsNullOrWhiteSpace(address.HostAuthority))
                throw new ArgumentException("Relative addresses are not supported by this instance");
            return address;
        }
    }

    internal class ClusterProxy : IClusterProxy
    {
        private readonly ProcessEndpointAddress m_address;

        public ClusterProxy(string hostAuthority)
        {
            m_address = ProcessEndpointAddress.Create(hostAuthority);
        }

        public event EventHandler ConnectionLost { add { } remove { } }

        public Task<bool> IsAlive()
        {
            return TaskCache.TrueTask;
        }
    }

    public interface IClusterProxy
    {
        event EventHandler ConnectionLost;

        Task<bool> IsAlive();
    }

    public class ProcessProxy
    {
        private readonly IClientConnectionFactory m_connectionFactory;
        public ITypeResolver TypeResolver { get; }

        public ProcessProxy()
            : this(true)
        {
        }

        public ProcessProxy(bool encryptConnections)
           : this(CreateTypeResolver(encryptConnections))
        {
        }

        private static ITypeResolver CreateTypeResolver(bool encryptConnections)
        {
            var typeResolver = DefaultTypeResolverFactory.DefaultTypeResolver.CreateNewScope();
            if (encryptConnections)
                typeResolver.RegisterFactory<IClientConnectionFactory>(r => new TcpTlsClientConnectionFactory(r));
            else
                typeResolver.RegisterFactory<IClientConnectionFactory>(r => new TcpClientConnectionFactory(r));
            return typeResolver;
        }

        public ProcessProxy(ITypeResolver resolver)
        {
            TypeResolver = resolver.CreateNewScope();
            m_connectionFactory = TypeResolver.CreateSingleton<IClientConnectionFactory>();
        }

        public static Task DestroyEndpoint(object endpointInstance)
        {
            var impl = ProcessProxyImplementation.Unwrap(endpointInstance);
            return impl.ParentProxy.DestroyEndpoint(impl.RemoteAddress);
        }

        public Task DestroyEndpoint(string address)
        {
            return DestroyEndpoint(ProcessEndpointAddress.Parse(address));
        }

        public Task DestroyEndpoint(ProcessEndpointAddress address)
        {
            var endpointBroker = address.ProcessAddress.Combine(WellKnownEndpoints.EndpointBroker);
            return CreateInterface<IEndpointBroker>(endpointBroker).DestroyEndpoint(address.EndpointId);
        }

        public static Task DestroyProcess(object proxyObject)
        {
            var impl = ProcessProxyImplementation.Unwrap(proxyObject);
            return impl.ParentProxy.DestroyProcess(impl.RemoteAddress);
        }

        public static Task SubscribeEventsAsync(Action subscriptionCallback)
        {
            return EventSubscriptionScope.SubscribeEventsAsync(subscriptionCallback);
        }

        public Task DestroyProcess(string address)
        {
            return DestroyProcess(ProcessEndpointAddress.Parse(address));
        }

        public Task DestroyProcess(ProcessEndpointAddress address)
        {
            var processBroker = address.ClusterAddress.Combine(WellKnownEndpoints.MasterProcessUniqueId, WellKnownEndpoints.ProcessBroker);
            return CreateInterface<IProcessBroker>(processBroker).DestroyProcess(address.ProcessId);
        }

        public T CreateInterface<T>(string address)
        {
            return CreateInterface<T>(ProcessEndpointAddress.Parse(address));
        }

        public T CreateInterface<T>(ProcessEndpointAddress address)
        {
            var proxy = ProcessProxyFactory.CreateImplementation<T>();
            proxy.Initialize(this, m_connectionFactory.GetConnection(address), address);
            return (T)(object)proxy;
        }

        public static ProcessEndpointAddress GetEndpointAddress(object proxyObject)
            => ProcessProxyImplementation.Unwrap(proxyObject).RemoteAddress;

        public IClusterProxy CreateClusterProxy(ProcessEndpointAddress addr) => CreateClusterProxy(addr.HostAuthority);
        public IClusterProxy CreateClusterProxy(string hostAuthority) => new ClusterProxy(hostAuthority);

        public static ValueTask PingAsync(object proxyObject)
        {
            var impl = ProcessProxyImplementation.Unwrap(proxyObject);
            return impl.PingAsync();
        }

        public static ValueTask SubscribeEndpointLost(object proxyObject, Action<EndpointLostEventArgs> handler)
        {
            var (handlerWrapper, s) = WrapSimpleHandler(handler);
            return SubscribeEndpointLost(proxyObject, handlerWrapper, s);
        }

        public static ValueTask SubscribeEndpointLost(object proxyObject, Action<EndpointLostEventArgs, object> handler, object state)
        {
            var impl = ProcessProxyImplementation.Unwrap(proxyObject);
            return impl.ParentProxy.SubscribeEndpointLost(impl.RemoteAddress, handler, state);
        }

        public ValueTask SubscribeEndpointLost(string address, Action<EndpointLostEventArgs> handler)
        {
            var (handlerWrapper, s) = WrapSimpleHandler(handler);
            return SubscribeEndpointLost(address, handlerWrapper, s);
        }

        public ValueTask SubscribeEndpointLost(string address, Action<EndpointLostEventArgs, object> handler, object state)
        {
            return SubscribeEndpointLost(ProcessEndpointAddress.Parse(address), handler, state);
        }

        public ValueTask SubscribeEndpointLost(ProcessEndpointAddress address, Action<EndpointLostEventArgs> handler)
        {
            var (handlerWrapper, s) = WrapSimpleHandler(handler);
            return SubscribeEndpointLost(address, handlerWrapper, s);
        }

        public ValueTask SubscribeEndpointLost(ProcessEndpointAddress address, Action<EndpointLostEventArgs, object> handler, object state)
        {
            return m_connectionFactory.GetConnection(address).SubscribeEndpointLost(address, handler, state);
        }

        public static void UnsubscribeEndpointLost(object proxyObject, Action<EndpointLostEventArgs> handler)
        {
            var (handlerWrapper, s) = WrapSimpleHandler(handler);
            UnsubscribeEndpointLost(proxyObject, handlerWrapper, s);
        }

        public static void UnsubscribeEndpointLost(object proxyObject, Action<EndpointLostEventArgs, object> handler, object state)
        {
            var impl = ProcessProxyImplementation.Unwrap(proxyObject);
            impl.ParentProxy.SubscribeEndpointLost(impl.RemoteAddress, handler, state).FireAndForget();
        }

        public void UnsubscribeEndpointLost(string address, Action<EndpointLostEventArgs> handler)
        {
            var (handlerWrapper, s) = WrapSimpleHandler(handler);
            SubscribeEndpointLost(ProcessEndpointAddress.Parse(address), handlerWrapper, s).FireAndForget();
        }

        public void UnsubscribeEndpointLost(string address, Action<EndpointLostEventArgs, object> handler, object state)
        {
            SubscribeEndpointLost(ProcessEndpointAddress.Parse(address), handler, state).FireAndForget();
        }

        public void UnsubscribeEndpointLost(ProcessEndpointAddress address, Action<EndpointLostEventArgs> handler)
        {
            var (handlerWrapper, s) = WrapSimpleHandler(handler);
            m_connectionFactory.GetConnection(address).UnsubscribeEndpointLost(address, handlerWrapper, s);
        }

        public void UnsubscribeEndpointLost(ProcessEndpointAddress address, Action<EndpointLostEventArgs, object> handler, object state)
        {
            m_connectionFactory.GetConnection(address).UnsubscribeEndpointLost(address, handler, state);
        }

        private static readonly Action<EndpointLostEventArgs, object> s_simpleHandlerWrapper = EndpointLostHandlerWithoutState;

        private static void EndpointLostHandlerWithoutState(EndpointLostEventArgs e, object state)
        {
            ((Action<EndpointLostEventArgs>)state)(e);
        }

        private static (Action<EndpointLostEventArgs, object> handler, object state) WrapSimpleHandler(Action<EndpointLostEventArgs> handler)
        {
            return (s_simpleHandlerWrapper, handler);
        }
    }
}
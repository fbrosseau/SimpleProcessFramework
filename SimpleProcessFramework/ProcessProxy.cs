using Spfx.Reflection;
using Spfx.Runtime.Client;
using Spfx.Runtime.Messages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx
{
    public class EventRegistrationRequestInfo
    {
        public class NewEventRegistration
        {
            public Action<EventRaisedMessage> Callback { get; }
            public string EventName { get; }

            public NewEventRegistration(string eventName, Action<EventRaisedMessage> callback)
            {
                EventName = eventName;
                Callback = callback;
            }
        }

        public EventRegistrationRequestInfo(ProcessEndpointAddress destination)
        {
            Destination = destination;
        }

        public List<NewEventRegistration> NewEvents { get; } = new List<NewEventRegistration>();
        public List<string> RemovedEvents { get; } = new List<string>();
        public ProcessEndpointAddress Destination { get; }
    }

    public interface IInterprocessRequestHandler
    {
        Task<object> ProcessCall(IClientInterprocessConnection targetConnection, RemoteInvocationRequest remoteCallRequest, CancellationToken ct = default);
        Task ChangeEventSubscription(IClientInterprocessConnection targetConnection, EventRegistrationRequestInfo req);
    }

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
            return new ProcessEndpointAddress(m_localAuthority, address.TargetProcess, address.LeafEndpoint);
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

    public class ProcessProxy
    {
        private readonly IClientConnectionFactory m_connectionFactory;
        private readonly ITypeResolver m_typeResolver;

        public ProcessProxy()
            : this(ProcessCluster.DefaultTypeResolver)
        {
        }

        public ProcessProxy(ITypeResolver resolver)
        {
            m_typeResolver = resolver.CreateNewScope();
            m_connectionFactory = m_typeResolver.GetSingleton<IClientConnectionFactory>();
        }

        public T CreateInterface<T>(ProcessEndpointAddress address)
        {
            address = NormalizeAddress(address);
            var proxy = ProcessProxyFactory.CreateImplementation<T>();
            proxy.Initialize(m_connectionFactory.GetConnection(address), address);
            return (T)(object)proxy;
        }

        public ProcessEndpointAddress NormalizeAddress(ProcessEndpointAddress address)
        {
            return m_connectionFactory.NormalizeAddress(address);
        }
    }
}
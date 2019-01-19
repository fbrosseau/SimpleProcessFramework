using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Client;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework
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

    public class ProcessProxy
    {
        private readonly IClientConnectionFactory m_connectionFactory;
        private readonly ITypeResolver m_typeResolver;

        public ProcessProxy()
            : this(ProcessCluster.DefaultTypeResolver.CreateNewScope())
        {
        }

        public ProcessProxy(ITypeResolver resolver)
        {
            m_typeResolver = resolver.CreateNewScope();
            m_connectionFactory = m_typeResolver.CreateSingleton<IClientConnectionFactory>();
        }

        public T CreateInterface<T>(ProcessEndpointAddress address)
        {
            var proxy = ProcessProxyFactory.CreateImplementation<T>();
            proxy.Initialize(m_connectionFactory.GetConnection(address), address);
            return (T)(object)proxy;
        }        
    }
}
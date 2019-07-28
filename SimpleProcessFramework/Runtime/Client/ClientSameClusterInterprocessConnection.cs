using Spfx.Reflection;
using Spfx.Runtime.Common;
using Spfx.Runtime.Messages;
using Spfx.Runtime.Server;
using Spfx.Serialization;
using Spfx.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client
{
    internal class ClientSameClusterInterprocessConnection : IClientInterprocessConnection, IInterprocessClientChannel
    {
        public event EventHandler ConnectionLost;

        private readonly IInternalMessageDispatcher m_localProcess;
        private readonly IInterprocessClientProxy m_proxyToThis;
        private readonly IBinarySerializer m_binarySerializer;
        private readonly IClientConnectionManager m_connectionManager;
        private readonly SimpleUniqueIdFactory<PendingClientCall> m_pendingRequests = new SimpleUniqueIdFactory<PendingClientCall>();
        private readonly SimpleUniqueIdFactory<Action<EventRaisedMessage>> m_eventRegistrations = new SimpleUniqueIdFactory<Action<EventRaisedMessage>>();

        public string UniqueId { get; }
        public IInterprocessClientProxy GetWrapperProxy() => m_proxyToThis;

        private class PendingClientCall : TaskCompletionSource<object>
        {
            public IInterprocessRequest Request { get; }

            public PendingClientCall(IInterprocessRequest req)
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
                Request = req;
            }
        }

        public ClientSameClusterInterprocessConnection(ITypeResolver typeResolver)
        {
            m_localProcess = typeResolver.GetSingleton<IInternalMessageDispatcher>();
            UniqueId = m_localProcess.LocalProcessUniqueId + "/0";

            m_proxyToThis = new ConcreteClientProxy(this);
            m_binarySerializer = typeResolver.GetSingleton<IBinarySerializer>();

            m_connectionManager = typeResolver.GetSingleton<IClientConnectionManager>();
        }

        Task IClientInterprocessConnection.ChangeEventSubscription(EventRegistrationRequestInfo req)
        {
            var outgoingMessage = new EventRegistrationRequest
            {
                Destination = req.Destination,
                RemovedEvents = req.RemovedEvents?.ToList()
            };

            if (req.NewEvents?.Count > 0)
            {
                outgoingMessage.AddedEvents = new List<EventRegistrationItem>();

                foreach (var evt in req.NewEvents)
                {
                    outgoingMessage.AddedEvents.Add(new EventRegistrationItem
                    {
                        EventName = evt.EventName,
                        RegistrationId = m_eventRegistrations.GetNextId(evt.Callback)
                    });
                }
            }

            return ((IClientInterprocessConnection)this).SerializeAndSendMessage(outgoingMessage);
        }

        public void Dispose()
        {
            ConnectionLost?.Invoke(this, EventArgs.Empty);
        }

        public ValueTask<ProcessEndpointMethodDescriptor> GetRemoteMethodDescriptor(ProcessEndpointAddress destination, ReflectedMethodInfo calledMethod)
        {
            throw new NotImplementedException();
        }

        void IInterprocessConnection.Initialize()
        {
            m_connectionManager.RegisterClientChannel(this);
        }

        void IInterprocessClientChannel.Initialize()
        {
            // TODO - Bad code
        }

        void IInterprocessClientChannel.SendMessage(IInterprocessMessage msg)
        {
            if (msg is WrappedInterprocessMessage wrapped)
                msg = wrapped.Unwrap(m_binarySerializer);

            switch (msg)
            {
                case RemoteInvocationResponse callResponse:
                    var completion = m_pendingRequests.RemoveById(callResponse.CallId);
                    callResponse.ForwardResult(completion);
                    break;
                case EventRaisedMessage eventMsg:
                    var handler = m_eventRegistrations.TryGetById(eventMsg.SubscriptionId);
                    handler?.Invoke(eventMsg);
                    break;
                default:
                    throw new InvalidOperationException("Message of type " + msg.GetType().FullName + " is not handled");
            }
        }

        Task<object> IInterprocessConnection.SerializeAndSendMessage(IInterprocessMessage msg, CancellationToken ct)
        {
            Task<object> completion = null;

            if (msg is IInterprocessRequest req && req.ExpectResponse)
            {
                var tcs = new PendingClientCall(req);
                req.CallId = m_pendingRequests.GetNextId(tcs);
                completion = tcs.Task;
            }

            m_localProcess.ForwardOutgoingMessage(this, msg, ct);
            return completion ?? BoxHelper.GetDefaultSuccessTask<object>();
        }
    }
}
using Spfx.Diagnostics.Logging;
using Spfx.Reflection;
using Spfx.Runtime.Common;
using Spfx.Runtime.Messages;
using Spfx.Runtime.Server;
using Spfx.Serialization;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client
{
    internal class ClientSameClusterInterprocessConnection : Disposable, IClientInterprocessConnection, IInterprocessClientChannel
    {
        public event EventHandler ConnectionLost;

        private readonly IInternalMessageDispatcher m_localProcess;
        private readonly IInterprocessClientProxy m_proxyToThis;
        private readonly IBinarySerializer m_binarySerializer;
        private readonly IClientConnectionManager m_connectionManager;
        private readonly SimpleUniqueIdFactory<PendingClientCall> m_pendingRequests = new SimpleUniqueIdFactory<PendingClientCall>();
        private readonly SimpleUniqueIdFactory<Action<EventRaisedMessage>> m_eventRegistrations = new SimpleUniqueIdFactory<Action<EventRaisedMessage>>();

        public string UniqueId { get; }

        private readonly ILogger m_logger;

        public IInterprocessClientProxy GetWrapperProxy() => m_proxyToThis;

        private class PendingClientCall : TaskCompletionSource<object>
        {
            public ClientSameClusterInterprocessConnection Owner { get; }
            public IInterprocessRequest Request { get; }
            private readonly CancellationToken m_ct;
            private readonly ProcessEndpointAddress m_originalAddress;

            public PendingClientCall(ClientSameClusterInterprocessConnection owner, IInterprocessRequest req, CancellationToken ct)
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
                Owner = owner;
                Request = req;
                m_ct = ct;
                m_originalAddress = req.Destination;
            }

            internal Task<object> ExecuteToCompletion()
            {
                if (!m_ct.CanBeCanceled)
                    return Task;

                return ExecuteWithCancellation();
            }

            private async Task<object> ExecuteWithCancellation()
            {
                using var reg = m_ct.Register(s => ((PendingClientCall)s).OnCancellationRequested(), this, false);
                return await Task;
            }

            private void OnCancellationRequested()
            {
                Owner.m_logger.Debug?.Trace("Call cancelled by source: " + Request.GetTinySummaryString());
                ((IInterprocessConnection)Owner).SerializeAndSendMessage(new RemoteCallCancellationRequest
                {
                    CallId = Request.CallId,
                    Destination = m_originalAddress
                }).FireAndForget();
            }
        }

        public ClientSameClusterInterprocessConnection(ITypeResolver typeResolver)
        {
            m_localProcess = typeResolver.GetSingleton<IInternalMessageDispatcher>();
            UniqueId = m_localProcess.LocalProcessUniqueId + "/0";

            m_logger = typeResolver.GetLogger(GetType(), uniqueInstance: true, friendlyName: UniqueId);

            m_proxyToThis = new ConcreteClientProxy(this);
            m_binarySerializer = typeResolver.CreateSingleton<IBinarySerializer>();

            m_connectionManager = typeResolver.GetSingleton<IClientConnectionManager>();
        }

        protected override void OnDispose()
        {
            m_logger.Info?.Trace("OnDispose");

            ConnectionLost?.Invoke(this, EventArgs.Empty);

            base.OnDispose();
            m_logger.Dispose();
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

        public ValueTask<ProcessEndpointMethodDescriptor> GetRemoteMethodDescriptor(ProcessEndpointAddress destination, ReflectedMethodInfo calledMethod)
        {
            throw new NotImplementedException();
        }

        void IInterprocessConnection.Initialize()
        {
            m_logger.Info?.Trace("Initialize");
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

            m_logger.Info?.Trace("SendMessage: " + msg.GetTinySummaryString());

            switch (msg)
            {
                case RemoteInvocationResponse callResponse:
                    var completion = m_pendingRequests.RemoveById(callResponse.GetValidCallId());
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
            PendingClientCall pendingCallState = null;

            m_logger.Info?.Trace("SerializeAndSendMessage: " + msg.GetTinySummaryString());

            if (msg is IInterprocessRequest req && (req.ExpectResponse || ct.CanBeCanceled))
            {
                pendingCallState = new PendingClientCall(this, req, ct);
                req.CallId = m_pendingRequests.GetNextId(pendingCallState);
            }

            m_localProcess.ForwardOutgoingMessage(this, msg);
            return pendingCallState?.ExecuteToCompletion() ?? TaskCache.NullObject;
        }
    }
}
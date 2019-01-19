using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Common;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Serialization;
using SimpleProcessFramework.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Client
{
    public interface IClientInterprocessConnection : IInterprocessConnection
    {
        ValueTask<ProcessEndpointMethodDescriptor> GetRemoteMethodDescriptor(ProcessEndpointAddress destination, ReflectedMethodInfo calledMethod);
        Task ChangeEventSubscription(EventRegistrationRequestInfo req);
    }

    internal abstract class AbstractClientInterprocessConnection : AbstractInterprocessConection, IClientInterprocessConnection
    {
        private readonly Dictionary<ProcessEndpointAddress, DescribedRemoteEndpoint> m_knownRemoteEndpoints = new Dictionary<ProcessEndpointAddress, DescribedRemoteEndpoint>();
        private readonly SimpleUniqueIdFactory<PendingOperation> m_pendingResponses = new SimpleUniqueIdFactory<PendingOperation>();
        private SimpleUniqueIdFactory<RemoteEventRegistration> m_eventRegistrations = new SimpleUniqueIdFactory<RemoteEventRegistration>();

        private class RemoteEventRegistration
        {
            public Action<EventRaisedMessage> Callback { get; }

            public RemoteEventRegistration(Action<EventRaisedMessage> callback)
            {
                Callback = callback;
            }
        }

        protected AbstractClientInterprocessConnection(IBinarySerializer serializer)
            : base(serializer)
        {
        }

        protected override void HandleExternalMessage(IInterprocessMessage msg)
        {
            msg = ((WrappedInterprocessMessage)msg).Unwrap(BinarySerializer);
            switch (msg)
            {
                case RemoteInvocationResponse callResponse:
                    PendingOperation op = m_pendingResponses.RemoveById(callResponse.CallId);
                    if (op is null)
                        return;

                    if (callResponse is RemoteCallSuccessResponse success)
                    {
                        op.Completion.TrySetResult(success.Result);
                    }
                    else if (callResponse is RemoteCallFailureResponse failure)
                    {
                        op.Completion.TrySetException(failure.Error);
                    }
                    else
                    {
                        HandleFailure(new SerializationException("Unexpected response"));
                    }

                    break;
                case EventRaisedMessage eventMsg:
                    var handler = m_eventRegistrations.TryGetById(eventMsg.SubscriptionId);
                    handler?.Callback(eventMsg);
                    break;
                default:
                    base.HandleExternalMessage(msg);
                    break;
            }
        }

        protected override async ValueTask DoWrite(PendingOperation op)
        {
            bool expectResponse = false;

            if (op.Request is IInterprocessRequest req && req.ExpectResponse)
            {
                expectResponse = true;

                op.Completion.Task.ContinueWith(t =>
                {
                    m_pendingResponses.RemoveById(req.CallId);
                }).FireAndForget();
            }

            await base.DoWrite(op).ConfigureAwait(false);

            if (!expectResponse)
            {
                op.Completion.TrySetResult(BoxHelper.BoxedInvalidType);
                op.Dispose();
            }
        }

        public override Task<object> SerializeAndSendMessage(IInterprocessMessage msg, CancellationToken ct = default)
        {
            var op = new PendingOperation(msg, ct);
            if (msg is IInterprocessRequest req)
            {
                req.CallId = m_pendingResponses.GetNextId(op);
            }

            return EnqueueOperation(op);
        }

        private class DescribedRemoteEndpoint
        {
            public Dictionary<ReflectedMethodInfo, ProcessEndpointMethodDescriptor> RemoteMethods;
        }

        public async ValueTask<ProcessEndpointMethodDescriptor> GetRemoteMethodDescriptor(ProcessEndpointAddress destination, ReflectedMethodInfo calledMethod)
        {
            DescribedRemoteEndpoint remoteDescription;
            lock (m_knownRemoteEndpoints)
            {
                m_knownRemoteEndpoints.TryGetValue(destination, out remoteDescription);
            }

            if (remoteDescription is null)
            {
                var rawDescriptor = await GetRemoteEndpointMetadata(destination, calledMethod.Type);
                remoteDescription = new DescribedRemoteEndpoint
                {
                    RemoteMethods = rawDescriptor.Methods.ToDictionary(m => m.Method)
                };

                lock (m_knownRemoteEndpoints)
                {
                    m_knownRemoteEndpoints[destination] = remoteDescription;
                }
            }

            return remoteDescription.RemoteMethods[calledMethod];
        }

        protected abstract Task<ProcessEndpointDescriptor> GetRemoteEndpointMetadata(ProcessEndpointAddress destination, ReflectedTypeInfo type);

        public Task ChangeEventSubscription(EventRegistrationRequestInfo req)
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
                        RegistrationId = m_eventRegistrations.GetNextId(new RemoteEventRegistration(evt.Callback))
                    });
                }
            }

            return SerializeAndSendMessage(outgoingMessage);
        }
    }
}
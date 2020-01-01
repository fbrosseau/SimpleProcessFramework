﻿using Spfx.Reflection;
using Spfx.Runtime.Common;
using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client
{
    public interface IClientInterprocessConnection : IInterprocessConnection
    {
        ValueTask<ProcessEndpointMethodDescriptor> GetRemoteMethodDescriptor(ProcessEndpointAddress destination, ReflectedMethodInfo calledMethod);
        Task ChangeEventSubscription(EventRegistrationRequestInfo req);
    }

    internal abstract class StreamBasedClientInterprocessConnection : StreamBasedInterprocessConnection, IClientInterprocessConnection
    {
        private readonly Dictionary<ProcessEndpointAddress, DescribedRemoteEndpoint> m_knownRemoteEndpoints = new Dictionary<ProcessEndpointAddress, DescribedRemoteEndpoint>();
        private readonly SimpleUniqueIdFactory<Action<EventRaisedMessage>> m_eventRegistrations = new SimpleUniqueIdFactory<Action<EventRaisedMessage>>();

        protected StreamBasedClientInterprocessConnection(ITypeResolver typeResolver)
            : base(typeResolver)
        {
        }

        protected override void ProcessReceivedMessage(IInterprocessMessage msg)
        {
            msg = msg.Unwrap(BinarySerializer);

            switch (msg)
            {
                case EventRaisedMessage eventMsg:
                    {
                        var handler = m_eventRegistrations.TryGetById(eventMsg.SubscriptionId);
                        handler?.Invoke(eventMsg);
                    }
                    break;
                default:
                    base.ProcessReceivedMessage(msg);
                    break;
            }
        }

        protected override async ValueTask DoWrite(PendingOperation op, Stream dataStream)
        {
            bool expectResponse = false;

            try
            {
                if (op.Message is IInterprocessRequest req && req.ExpectResponse)
                {
                    expectResponse = true;

                    op.Task.ContinueWith(t =>
                    {
                        DiscardPendingRequest(req.CallId);
                    }).FireAndForget();
                }

                await base.DoWrite(op, dataStream).ConfigureAwait(false);

                if (!expectResponse)
                {
                    op.TrySetResult(BoxHelper.BoxedInvalidType);
                    op.Dispose();
                }
            }
            catch
            {
                op.Dispose();
                throw;
            }
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
                        RegistrationId = m_eventRegistrations.GetNextId(evt.Callback)
                    });
                }
            }

            return SerializeAndSendMessage(outgoingMessage);
        }
    }
}
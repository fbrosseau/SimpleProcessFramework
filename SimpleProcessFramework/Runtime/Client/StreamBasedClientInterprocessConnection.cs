using Spfx.Reflection;
using Spfx.Runtime.Client.Eventing;
using Spfx.Runtime.Common;
using Spfx.Runtime.Messages;
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
        
        ValueTask ChangeEventSubscription(EventSubscriptionChangeRequest req);

        ValueTask SubscribeEndpointLost(ProcessEndpointAddress address, EventHandler<EndpointLostEventArgs> handler);
        void UnsubscribeEndpointLost(ProcessEndpointAddress address, EventHandler<EndpointLostEventArgs> handler);
    }

    internal abstract class StreamBasedClientInterprocessConnection : StreamBasedInterprocessConnection, IClientInterprocessConnection
    {
        private class DescribedRemoteEndpoint
        {
            public Dictionary<ReflectedMethodInfo, ProcessEndpointMethodDescriptor> RemoteMethods;
        }

        protected ProcessEndpointAddress Destination { get; }

        private readonly Dictionary<ProcessEndpointAddress, DescribedRemoteEndpoint> m_knownRemoteEndpoints = new Dictionary<ProcessEndpointAddress, DescribedRemoteEndpoint>();
        private readonly EventSubscriptionManager m_eventManager;

        protected StreamBasedClientInterprocessConnection(ProcessEndpointAddress destination, ITypeResolver typeResolver)
            : base(typeResolver)
        {
            Destination = destination;
            m_eventManager = new EventSubscriptionManager(typeResolver, this, Destination);
        }

        protected override void ProcessReceivedMessage(IInterprocessMessage msg)
        {
            msg = msg.Unwrap(BinarySerializer);
            Logger.Debug?.Trace("ProcessReceivedMessage: " + msg.GetTinySummaryString());
            if (!m_eventManager.ProcessIncomingMessage(msg))
                base.ProcessReceivedMessage(msg);
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

        ValueTask IClientInterprocessConnection.SubscribeEndpointLost(ProcessEndpointAddress address, EventHandler<EndpointLostEventArgs> handler)
        {
            return m_eventManager.SubscribeEndpointLost(address, handler);
        }

        void IClientInterprocessConnection.UnsubscribeEndpointLost(ProcessEndpointAddress address, EventHandler<EndpointLostEventArgs> handler)
        {
            m_eventManager.UnsubscribeEndpointLost(address, handler);
        }

        ValueTask IClientInterprocessConnection.ChangeEventSubscription(EventSubscriptionChangeRequest req)
        {
            return m_eventManager.ChangeEventSubscription(req);
        }

        public override string ToString() => $"{GetType().Name}->" + Destination;
    }
}
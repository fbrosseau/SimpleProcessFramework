using Spfx.Io;
using Spfx.Reflection;
using Spfx.Runtime.Client.Events;
using Spfx.Runtime.Common;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Messages;
using Spfx.Runtime.Server.Listeners;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client
{
    public interface IClientInterprocessConnection : IInterprocessConnection
    {
        ProcessEndpointAddress Destination { get; }

        ValueTask<ProcessEndpointMethodDescriptor> GetRemoteMethodDescriptor(ProcessEndpointAddress destination, ReflectedMethodInfo calledMethod);

        ValueTask ChangeEventSubscription(EventSubscriptionChangeRequest req);

        ValueTask SubscribeEndpointLost(ProcessEndpointAddress address, Action<EndpointLostEventArgs, object> handler, object state);
        void UnsubscribeEndpointLost(ProcessEndpointAddress address, Action<EndpointLostEventArgs, object> handler, object state);
    }

    internal abstract class StreamBasedClientInterprocessConnection : StreamBasedInterprocessConnection, IClientInterprocessConnection
    {
        private class DescribedRemoteEndpoint
        {
            public Dictionary<ReflectedMethodInfo, ProcessEndpointMethodDescriptor> RemoteMethods;
        }

        public ProcessEndpointAddress Destination { get; }

        private readonly Dictionary<ProcessEndpointAddress, DescribedRemoteEndpoint> m_knownRemoteEndpoints = new Dictionary<ProcessEndpointAddress, DescribedRemoteEndpoint>();
        private readonly EventSubscriptionManager m_eventManager;

        protected StreamBasedClientInterprocessConnection(ProcessEndpointAddress destination, ITypeResolver typeResolver)
            : base(typeResolver, destination.ToString())
        {
            Destination = destination;
            m_eventManager = new EventSubscriptionManager(typeResolver, this, Destination);
        }

        protected override void OnDispose()
        {
            base.OnDispose();
            m_eventManager.Dispose();
        }

        protected override async ValueTask OnTeardownAsync(CancellationToken ct = default)
        {
            await m_eventManager.TeardownAsync(ct).ConfigureAwait(false);
            await base.OnTeardownAsync(ct).ConfigureAwait(false);
        }

        protected override void HandleFailure(Exception ex)
        {
            base.HandleFailure(ex);
            m_eventManager.HandleConnectionFailure(ex);
        }

        protected async Task Authenticate(Stream dataStream)
        {
            await dataStream.WriteAsync(
                StreamInterprocessConnectionListener.MagicStartCodeBytes,
                0,
                StreamInterprocessConnectionListener.MagicStartCodeBytes.Length).ConfigureAwait(false);

            using (var hello = BinarySerializer.Serialize<object>(new RemoteClientConnectionRequest(), lengthPrefix: true))
            {
                await hello.CopyToAsync(dataStream).ConfigureAwait(false);
            }

            using var responseStream = await dataStream.ReadLengthPrefixedBlockAsync().ConfigureAwait(false);
            var response = (RemoteClientConnectionResponse)BinarySerializer.Deserialize<object>(responseStream);

            if (response.Success)
                return;

            throw new ProxyConnectionAuthenticationFailedException(string.IsNullOrWhiteSpace(response.Error) ? "The connection was refused by the remote host" : response.Error);
        }

        protected override void ProcessReceivedMessage(IInterprocessMessage msg)
        {
            msg = msg.Unwrap(BinarySerializer);
            Logger.Debug?.Trace("ProcessReceivedMessage: " + msg.GetTinySummaryString());
            if (!m_eventManager.ProcessIncomingMessage(msg))
                base.ProcessReceivedMessage(msg);
        }

        protected sealed class ClientPendingOperation<TResult> : PendingStreamOperation<TResult>
        {
            private new StreamBasedClientInterprocessConnection Owner => (StreamBasedClientInterprocessConnection)base.Owner;

            private static readonly Action<EndpointLostEventArgs, object> s_endpointLostHandler = OnPendingOperationRemoteEndpointLost;

            private static void OnPendingOperationRemoteEndpointLost(EndpointLostEventArgs e, object state)
            {
                ((ClientPendingOperation<TResult>)state).OnEndpointLost(e);
            }

            public ClientPendingOperation(StreamBasedClientInterprocessConnection owner, IInterprocessMessage req, CancellationToken ct)
                : base(owner, req, ct)
            {
                owner.m_eventManager.SubscribeEndpointLost(MessageDestination, s_endpointLostHandler, this).FireAndForget();
            }

            private void OnEndpointLost(EndpointLostEventArgs e)
            {
                Owner.Logger.Debug?.Trace("Aborting call due to endpoint lost [" + ToString() + "] Reason=" + e.Reason);
                TrySetException(new EndOfStreamException());
            }

            public override void Dispose()
            {
                base.Dispose();
                Owner.m_eventManager.UnsubscribeEndpointLost(MessageDestination, s_endpointLostHandler, this);
            }
        }

        protected sealed override IPendingOperation CreatePendingOperation<TResult>(IInterprocessMessage msg, CancellationToken ct)
        {
            return new ClientPendingOperation<TResult>(this, msg, ct);
        }

        protected override async ValueTask DoWrite(IPendingOperation op, Stream dataStream)
        {
            bool expectResponse = false;

            try
            {
                if (op.Message is IInterprocessRequest req && req.ExpectResponse)
                {
                    expectResponse = true;

                    op.Task.ContinueWith((t, s) =>
                    {
                        var innerOp = (IPendingOperation)s;
                        var innerThis = (StreamBasedClientInterprocessConnection)innerOp.Owner;
                        innerThis.DiscardPendingRequest(((IInterprocessRequest)innerOp.Message).CallId);
                    }, op, TaskScheduler.Default).FireAndForget();
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
                var rawDescriptor = await GetRemoteEndpointMetadata(destination, calledMethod.Type).ConfigureAwait(false);
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

        ValueTask IClientInterprocessConnection.SubscribeEndpointLost(ProcessEndpointAddress address, Action<EndpointLostEventArgs, object> handler, object state)
        {
            return m_eventManager.SubscribeEndpointLost(address, handler, state);
        }

        void IClientInterprocessConnection.UnsubscribeEndpointLost(ProcessEndpointAddress address, Action<EndpointLostEventArgs, object> handler, object state)
        {
            m_eventManager.UnsubscribeEndpointLost(address, handler, state);
        }

        ValueTask IClientInterprocessConnection.ChangeEventSubscription(EventSubscriptionChangeRequest req)
        {
            return m_eventManager.ChangeEventSubscription(req);
        }

        public override string ToString() => $"{GetType().Name}->" + Destination;
    }
}
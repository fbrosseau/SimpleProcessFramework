using Spfx.Diagnostics;
using Spfx.Diagnostics.Logging;
using Spfx.Reflection;
using Spfx.Runtime.Common;
using Spfx.Runtime.Messages;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client.Eventing
{
    internal class EventSubscriptionManager : AsyncDestroyable
    {
        private readonly AsyncThread m_subscriptionThread;
        private readonly AsyncThread m_eventRaisingThread;
        private readonly IInterprocessConnection m_connection;
        private readonly ITypeResolver m_typeResolver;

        public ProcessEndpointAddress RemoteAddress { get; }
        private ILogger Logger { get; }

        private class EndpointEventSubscriptionState
        {
            public EndpointEventSubscriptionState Parent { get; }
            public ProcessEndpointAddress Address { get; }
            public List<EndpointEventSubscriptionState> Children { get; } = new List<EndpointEventSubscriptionState>();
            public Dictionary<string, RawEventDelegate> CallbackHandlers { get; } = new Dictionary<string, RawEventDelegate>();

            private Action m_lostHandler;

            public EndpointEventSubscriptionState(EndpointEventSubscriptionState parent, ProcessEndpointAddress remoteAddress)
            {
                Parent = parent;
                Address = remoteAddress;
            }

            public void AddLostHandler(Action a)
            {
                m_lostHandler += a;
            }

            public void RemoveLostHandler(Action a)
            {
                m_lostHandler -= a;
            }

            internal void RaiseLostEvent()
            {
                m_lostHandler?.Invoke();
            }

            public override string ToString()
            {
                return $"{Address} ({CallbackHandlers.Count} subscriptions)";
            }
        }

        private readonly EndpointEventSubscriptionState m_root;
        private readonly Dictionary<ProcessEndpointAddress, EndpointEventSubscriptionState> m_knownEndpoints;

        public EventSubscriptionManager(ITypeResolver typeResolver, IInterprocessConnection parent, ProcessEndpointAddress remoteAddress)
        {
            Guard.ArgumentNotNull(remoteAddress, nameof(remoteAddress));
            RemoteAddress = remoteAddress;
            Logger = typeResolver.GetLogger(GetType(), uniqueInstance: true, friendlyName: remoteAddress.ToString());
            m_connection = parent;
            m_typeResolver = typeResolver;
            m_subscriptionThread = new AsyncThread(allowSyncCompletion: true);
            m_root = new EndpointEventSubscriptionState(null, RemoteAddress.ClusterAddress);
            m_eventRaisingThread = new AsyncThread(allowSyncCompletion: false);

            m_knownEndpoints = new Dictionary<ProcessEndpointAddress, EndpointEventSubscriptionState>(ProcessEndpointAddress.RelativeAddressComparer)
            {
                { m_root.Address, m_root }
            };
        }

        protected override async ValueTask OnTeardownAsync(CancellationToken ct = default)
        {
            await m_subscriptionThread.TeardownAsync(ct).ConfigureAwait(false);
            await m_eventRaisingThread.TeardownAsync(ct).ConfigureAwait(false);
            await base.OnTeardownAsync(ct);
        }

        protected override void OnDispose()
        {
            m_subscriptionThread.Dispose();
            m_eventRaisingThread.Dispose();
            base.OnDispose();
        }

        internal ValueTask ChangeEventSubscription(EventSubscriptionChangeRequest req)
        {
            return m_subscriptionThread.ExecuteTaskAsync(() => ChangeEventSubscriptionInternal(req));
        }

        private async Task ChangeEventSubscriptionInternal(EventSubscriptionChangeRequest req)
        {
            var messagesPerDestinationProcess = new Dictionary<ProcessEndpointAddress, EventRegistrationRequest>();

            foreach (var change in req.Changes)
            {
                if (!ApplyNewChange(change))
                    continue;

                var ep = change.Endpoint;
                if (!messagesPerDestinationProcess.TryGetValue(ep, out var processRequest))
                {
                    messagesPerDestinationProcess[ep] = processRequest = new EventRegistrationRequest
                    {
                        Destination = ep
                    };
                }

                if (change.IsAdd)
                {
                    if (processRequest.AddedEvents is null)
                        processRequest.AddedEvents = new List<string>();
                    processRequest.AddedEvents.Add(change.Name);
                }
                else
                {
                    if (processRequest.RemovedEvents is null)
                        processRequest.RemovedEvents = new List<string>();
                    processRequest.RemovedEvents.Add(change.Name);
                }
            }

            var tasks = new List<Task>();

            foreach (var msg in messagesPerDestinationProcess.Values)
            {
                tasks.Add(m_connection.SerializeAndSendMessage(msg));
            }

            await TaskEx.WhenAllOrRethrow(tasks).ConfigureAwait(false);
        }

        internal void UnsubscribeEndpointLost(ProcessEndpointAddress address, EventHandler<EndpointLostEventArgs> handler)
        {
        }

        private bool ApplyNewChange(EventSubscriptionChange change)
        {
            lock (m_knownEndpoints)
            {
                var entry = GetEntry(change.Endpoint, createIfMissing: change.IsAdd);
                if (entry is null)
                    return false;

                if (change.IsAdd)
                {
                    bool isNew = !entry.CallbackHandlers.TryGetValue(change.Name, out var h);
                    h += change.Handler;
                    entry.CallbackHandlers[change.Name] = h;

                    return isNew;
                }
                else
                {
                    if (!entry.CallbackHandlers.TryGetValue(change.Name, out var h))
                        return false;

                    h -= change.Handler;
                    if (h is null)
                    {
                        entry.CallbackHandlers.Remove(change.Name);
                        return true;
                    }
                    else
                    {
                        entry.CallbackHandlers[change.Name] = h;
                        return false;
                    }
                }
            }
        }

        private EndpointEventSubscriptionState GetEntry(ProcessEndpointAddress endpoint, bool createIfMissing)
        {
            AssertIsLocked();

            if (m_knownEndpoints.TryGetValue(endpoint, out var s) || !createIfMissing)
                return s;

            var parent = m_root;
            if (endpoint.EndpointId != null)
                parent = GetEntry(endpoint.ProcessAddress, createIfMissing);

            s = new EndpointEventSubscriptionState(parent, endpoint);
            parent.Children.Add(s);

            m_knownEndpoints[endpoint] = s;
            return s;
        }

        internal ValueTask SubscribeEndpointLost(ProcessEndpointAddress address, EventHandler<EndpointLostEventArgs> handler)
        {
            return default;
            //return m_subscriptionThread.ExecuteAsync(() => ChangeEventSubscriptionInternal(req));
        }

        internal bool ProcessIncomingMessage(IInterprocessMessage msg)
        {
            switch (msg)
            {
                case EndpointLostMessage epLost:
                    HandleEndpointLost(epLost);
                    return true;
                case EventRaisedMessage evtReceived:
                    HandleEventReceived(evtReceived);
                    return true;
                default:
                    return false;
            }
        }

        private void HandleEventReceived(EventRaisedMessage evtReceived)
        {
            Logger.Debug?.Trace(nameof(HandleEventReceived) + ": " + evtReceived.GetTinySummaryString());

            RawEventDelegate h;
            lock (m_knownEndpoints)
            {
                if (!m_knownEndpoints.TryGetValue(evtReceived.EndpointId, out var s)
                    || !s.CallbackHandlers.TryGetValue(evtReceived.EventName, out h))
                    return;
            }

            m_eventRaisingThread.QueueAction(state =>
            {
                CriticalTryCatch.Run(m_typeResolver,
                    (state.h, state.evtReceived),
                    state2 => state2.h.Invoke(state.evtReceived.EventArgs));
            }, (m_typeResolver, h, evtReceived));
        }

        private void HandleEndpointLost(EndpointLostMessage epLost)
        {
            Logger.Debug?.Trace(nameof(HandleEndpointLost) + ": " + epLost.GetTinySummaryString());

            lock (m_knownEndpoints)
            {
                DiscardRegistrations(epLost.Endpoint);
            }
        }

        private void DiscardRegistrations(ProcessEndpointAddress ep)
        {
            AssertIsLocked();

            if (!m_knownEndpoints.Remove(ep, out var s))
                return;

            s.RaiseLostEvent();

            s.Parent?.Children.Remove(s);

            foreach (var child in s.Children)
            {
                DiscardRegistrations(child.Address);
            }
        }

        [Conditional("DEBUG")]
        private void AssertIsLocked()
        {
            Debug.Assert(Monitor.IsEntered(m_knownEndpoints));
        }
    }
}
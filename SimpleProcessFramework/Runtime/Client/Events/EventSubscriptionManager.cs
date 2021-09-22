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
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client.Events
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
            public EventSubscriptionManager Owner { get; }
            public EndpointEventSubscriptionState Parent { get; }
            public ProcessEndpointAddress Address { get; }
            public List<EndpointEventSubscriptionState> Children { get; } = new();
            public Dictionary<string, RawEventDelegate> CallbackHandlers { get; } = new();

            private HashSet<(Action<EndpointLostEventArgs, object>, object)> m_lostHandlers;

            public EndpointEventSubscriptionState(EventSubscriptionManager owner, EndpointEventSubscriptionState parent, ProcessEndpointAddress remoteAddress)
            {
                Owner = owner;
                Parent = parent;
                Address = remoteAddress;
            }

            public void AddLostHandler(Action<EndpointLostEventArgs, object> a, object state)
            {
                m_lostHandlers ??= new HashSet<(Action<EndpointLostEventArgs, object>, object)>();
                m_lostHandlers.Add((a, state));
            }

            public void RemoveLostHandler(Action<EndpointLostEventArgs, object> a, object state)
            {
                m_lostHandlers?.Remove((a, state));
            }

            internal void RaiseLostEvent(ProcessEndpointAddress originalAddress, ProcessEndpointAddress currentAddress, EndpointLostReason reason)
            {
                if (m_lostHandlers is null || m_lostHandlers.Count == 0)
                    return;

                var eventArgs = new EndpointLostEventArgs(originalAddress, currentAddress, reason);

                foreach (var (action, state) in m_lostHandlers)
                {
                    CriticalTryCatch.Run(Owner.m_typeResolver, (eventArgs, action, state), s =>
                    {
                        s.action(s.eventArgs, s.state);
                    });
                }
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
            m_root = new EndpointEventSubscriptionState(this, null, RemoteAddress.ClusterAddress);
            m_eventRaisingThread = new AsyncThread(allowSyncCompletion: false);

            m_knownEndpoints = new Dictionary<ProcessEndpointAddress, EndpointEventSubscriptionState>(ProcessEndpointAddress.RelativeAddressComparer)
            {
                { m_root.Address, m_root }
            };
        }

        protected override async ValueTask OnTeardownAsync(CancellationToken ct = default)
        {
            RaiseAllEndpointsLost(null);
            await m_subscriptionThread.TeardownAsync(ct).ConfigureAwait(false);
            await m_eventRaisingThread.TeardownAsync(ct).ConfigureAwait(false);
            await base.OnTeardownAsync(ct).ConfigureAwait(false);
        }

        protected override void OnDispose()
        {
            RaiseAllEndpointsLost(null);
            m_subscriptionThread.Dispose();
            m_eventRaisingThread.Dispose();
            base.OnDispose();
        }

        internal void HandleConnectionFailure(Exception ex)
        {
            RaiseAllEndpointsLost(ex);
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

            s = new EndpointEventSubscriptionState(this, parent, endpoint);
            parent.Children.Add(s);

            m_knownEndpoints[endpoint] = s;
            return s;
        }

        internal ValueTask SubscribeEndpointLost(ProcessEndpointAddress address, Action<EndpointLostEventArgs, object> handler, object state)
        {
            lock (m_knownEndpoints)
            {
                var entry = GetEntry(address, createIfMissing: true);
                entry.AddLostHandler(handler, state);
            }

            return default;
            //return m_subscriptionThread.ExecuteAsync(() => ChangeEventSubscriptionInternal(req));
        }

        internal void UnsubscribeEndpointLost(ProcessEndpointAddress address, Action<EndpointLostEventArgs, object> handler, object state)
        {
            lock (m_knownEndpoints)
            {
                var entry = GetEntry(address, createIfMissing: false);
                entry?.RemoveLostHandler(handler, state);
            }
        }

        private void RaiseAllEndpointsLost(Exception ex)
        {
            lock (m_knownEndpoints)
            {
                DiscardRegistrations(m_root.Address, m_root.Address, EndpointLostReason.RemoteEndpointLost);
            }
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
                DiscardRegistrations(epLost.Endpoint, epLost.Endpoint, EndpointLostReason.RemoteEndpointLost);
            }
        }

        private void DiscardRegistrations(ProcessEndpointAddress originalAddress, ProcessEndpointAddress currentAddress, EndpointLostReason reason)
        {
            AssertIsLocked();

            if (!m_knownEndpoints.Remove(currentAddress, out var s))
                return;

            s.RaiseLostEvent(originalAddress, currentAddress, reason);

            s.Parent?.Children.Remove(s);

            foreach (var child in s.Children.ToArray())
            {
                DiscardRegistrations(originalAddress, child.Address, reason);
            }
        }

        [Conditional("DEBUG")]
        private void AssertIsLocked()
        {
            Debug.Assert(Monitor.IsEntered(m_knownEndpoints));
        }
    }
}
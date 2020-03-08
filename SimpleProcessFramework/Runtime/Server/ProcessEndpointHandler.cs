using Spfx.Diagnostics.Logging;
using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Messages;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server
{
    [DataContract]
    public abstract class ProcessEndpointHandler : AsyncDestroyable, IProcessEndpointHandler
    {
        public object ImplementationObject { get; }

        private readonly IProcessEndpoint m_realTargetAsEndpoint;
        private readonly ProcessEndpointDescriptor m_descriptor;
        private readonly Dictionary<PendingCallKey, IInterprocessRequestContext> m_pendingCalls = new Dictionary<PendingCallKey, IInterprocessRequestContext>();
        private readonly ProcessEndpointMethodDescriptor[] m_methods;
        private readonly Dictionary<string, ProcessEndpointMethodDescriptor> m_methodsByName;
        private readonly Dictionary<string, EventSubscriptionInfo> m_eventsByName = new Dictionary<string, EventSubscriptionInfo>();
        private readonly ILogger m_logger;

        private ProcessEndpointAddress EndpointAddress { get; }
        private IProcess ParentProcess { get; }

        protected ProcessEndpointHandler(IProcess parentProcess, string endpointId, object realTarget, ProcessEndpointDescriptor descriptor)
        {
            ParentProcess = parentProcess;
            EndpointAddress = parentProcess.UniqueAddress.Combine(endpointId);
            ImplementationObject = realTarget;
            m_realTargetAsEndpoint = ImplementationObject as IProcessEndpoint;
            m_descriptor = descriptor;
            m_methods = m_descriptor.Methods.ToArray();
            m_methodsByName = m_methods.ToDictionary(m => m.Method.GetUniqueName());
            m_logger = NullLogger.Logger; // TODO
        }

        protected override void OnDispose()
        {
            m_logger.Info?.Trace("OnDispose");

            CancelAllCalls();
            (ImplementationObject as IDisposable)?.Dispose();
            base.OnDispose();
            m_logger.Dispose();
        }

        protected override async ValueTask OnTeardownAsync(CancellationToken ct)
        {
            m_logger.Info?.Trace("OnTeardownAsync");

            if (ImplementationObject is IAsyncDestroyable d)
                await d.TeardownAsync(ct).ConfigureAwait(false);

            await base.OnTeardownAsync(ct);
        }

        public async ValueTask InitializeAsync()
        {
            m_logger.Info?.Trace("InitializeAsync");

            if (m_realTargetAsEndpoint != null)
                await m_realTargetAsEndpoint.InitializeAsync(ParentProcess, EndpointAddress).ConfigureAwait(false);
        }
        
        private void CancelAllCalls()
        {
            m_logger.Info?.Trace("CancelAllCalls");
            List<IInterprocessRequestContext> requests;
            lock (m_pendingCalls)
            {
                requests = m_pendingCalls.Values.ToList();
                m_pendingCalls.Clear();
            }

            foreach (var pending in requests)
            {
                pending.Cancel();
            }
        }

        protected void PrepareEvent(ReflectedEventInfo evt)
        {
            m_eventsByName.Add(evt.Name, new EventSubscriptionInfo(EndpointAddress, ImplementationObject, evt));
        }

        public void HandleMessage(IInterprocessRequestContext req)
        {
            try
            {
                if (m_realTargetAsEndpoint?.FilterMessage(req) == false)
                {
                    m_logger.Debug?.Trace("Request filtered out: " + req.Request.GetTinySummaryString());
                    return;
                }

                m_logger.Debug?.Trace("Dispatching request: " + req.Request.GetTinySummaryString());

                switch (req.Request)
                {
                    case EndpointDescriptionRequest _:
                        req.Respond(m_descriptor);
                        break;
                    case RemoteCallRequest call:
                        DoRemoteCall(req, call);
                        break;
                    case RemoteCallCancellationRequest _:
                        CancelCall(req);
                        break;
                    case EventRegistrationRequest evt:
                        UpdateEventSubscriptions(req, evt);
                        break;
                    default:
                        req.Fail(new InvalidOperationException("Cannot handle request"));
                        break;
                }
            }
            catch (Exception ex)
            {
                req.Fail(ex);
            }
        }

        private void CancelCall(IInterprocessRequestContext req)
        {
            var key = new PendingCallKey(req);
            IInterprocessRequestContext existingCall;

            lock (m_pendingCalls)
            {
                // we don't actually remove the call here, the only path that removes calls is the actual completion/failure.
                m_pendingCalls.TryGetValue(key, out existingCall);
            }

            if (existingCall != null)
            {
                m_logger.Debug?.Trace("Cancelling request: " + existingCall.Request.GetTinySummaryString());
                existingCall.Cancel();
            }
            else
            {
                m_logger.Debug?.Trace("Received cancel for unknown request " + key);
            }
        }

        private void DoRemoteCall(IInterprocessRequestContext req, IRemoteCallRequest remoteCall)
        {
            if (remoteCall.MethodName != null)
            {
                if (!m_methodsByName.TryGetValue(remoteCall.MethodName, out var m))
                    ThrowBadInvocation("Method not found: " + remoteCall.MethodName);

                remoteCall.MethodId = m.MethodId;
            }

            if (remoteCall.MethodId < 0 || remoteCall.MethodId >= m_methods.Length)
                ThrowBadInvocation("Method ID is unknown");

            var method = m_methods[remoteCall.MethodId];
            var argsCount = remoteCall.ArgsCount;

            if (argsCount != method.Method.GetArgumentCount())
                ThrowBadInvocation($"Method {method.MethodId} expected {method.Method.GetArgumentCount()} parameters - {argsCount} provided");

            var key = new PendingCallKey(req);
            lock (m_pendingCalls)
            {
                m_pendingCalls.Add(key, req);
            }

            if (method.IsCancellable)
                req.SetupCancellationToken();

            DoRemoteCallImpl(req);
        }

        protected class EventSubscriptionInfo
        {
            private class ClientEventRegistration
            {
                public IInterprocessClientProxy Client;
                public long RegistrationId;
            }

            public ReflectedEventInfo Event { get; }
            private List<ClientEventRegistration> m_listeners = new List<ClientEventRegistration>();
            private Delegate m_eventHandler;

            private static readonly MethodInfo s_eventHandlerMethod = typeof(EventSubscriptionInfo).FindUniqueMethod(nameof(OnEventRaised));
            private readonly ProcessEndpointAddress m_endpointAddress;
            private readonly object m_target;

            private Delegate CreateEventHandler()
            {
                if (m_eventHandler is null)
                    m_eventHandler = Delegate.CreateDelegate(Event.ResolvedEvent.EventHandlerType, this, s_eventHandlerMethod);
                return m_eventHandler;
            }

            public EventSubscriptionInfo(ProcessEndpointAddress endpointAddress, object target, ReflectedEventInfo eventInfo)
            {
                m_endpointAddress = endpointAddress;
                m_target = target;
                Event = eventInfo;
            }

            internal void AddConsumer(IInterprocessClientProxy client, long registrationId)
            {
                bool newSub;
                lock (this)
                {
                    newSub = m_listeners.Count == 0;
                    var newList = new List<ClientEventRegistration>(m_listeners.Count + 1);
                    newList.AddRange(m_listeners);
                    newList.Add(new ClientEventRegistration { Client = client, RegistrationId = registrationId });
                    m_listeners = newList;
                }

                if (newSub)
                {
                    Event.ResolvedEvent.AddEventHandler(m_target, CreateEventHandler());
                }
            }

            internal void RemoveConsumer(IInterprocessClientProxy client)
            {
                bool unsubscribe;
                lock (this)
                {
                    var newList = m_listeners.ToList();
                    newList.RemoveAll(r => r.Client == client);
                    m_listeners = newList;
                    unsubscribe = m_listeners.Count == 0;
                }

                if (unsubscribe)
                {
                    Event.ResolvedEvent.RemoveEventHandler(m_target, CreateEventHandler());
                }
            }

            public void OnEventRaised(object sender, object args)
            {
                var listeners = m_listeners;
                foreach (var l in listeners)
                {
                    l.Client.SendMessage(new EventRaisedMessage
                    {
                        EndpointId = m_endpointAddress,
                        SubscriptionId = l.RegistrationId,
                        EventName = Event.Name,
                        EventArgs = args
                    });
                }
            }
        }

        private void UpdateEventSubscriptions(IInterprocessRequestContext req, EventRegistrationRequest evt)
        {
            if (evt.AddedEvents?.Count > 0)
            {
                foreach (var e in evt.AddedEvents)
                {
                    if (!m_eventsByName.TryGetValue(e, out var eventInfo))
                        ThrowBadInvocation("Event does not exist: " + e);

                    eventInfo.AddConsumer(req.Client, -1);
                }
            }

            if (evt.RemovedEvents?.Count > 0)
            {
                foreach (var e in evt.RemovedEvents)
                {
                    if (!m_eventsByName.TryGetValue(e, out var eventInfo))
                        ThrowBadInvocation("Event does not exist: " + e);

                    eventInfo.RemoveConsumer(req.Client);
                }
            }

            req.Respond(null);
        }

        protected void ThrowBadInvocation(string reason)
        {
            throw new BadMethodInvocationException(reason);
        }

        protected void ThrowBadInvocationWithoutText()
        {
            ThrowBadInvocation("Unknown method");
        }

        void IProcessEndpointHandler.CompleteCall(IInterprocessRequestContext req)
        {
            var key = new PendingCallKey(req);
            lock (m_pendingCalls)
            {
                m_pendingCalls.Remove(key);
            }
        }

        protected abstract void DoRemoteCallImpl(IInterprocessRequestContext callRequest); // codegen

        #region Inner classes
        internal static class Reflection
        {
            public static MethodInfo DoRemoteCallImplMethod => typeof(ProcessEndpointHandler).FindUniqueMethod(nameof(DoRemoteCallImpl));
            public static MethodInfo ThrowBadInvocationWithoutTextMethod => typeof(ProcessEndpointHandler).FindUniqueMethod(nameof(ThrowBadInvocationWithoutText));
            public static MethodInfo PrepareEventMethod => typeof(ProcessEndpointHandler).FindUniqueMethod(nameof(PrepareEvent));
            public static ConstructorInfo Constructor => typeof(ProcessEndpointHandler).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Single();
        }

        private struct PendingCallKey : IEquatable<PendingCallKey>
        {
            public IInterprocessClientProxy Client { get; }
            public long CallId { get; }

            public PendingCallKey(IInterprocessRequestContext req)
                : this(req.Client, req.Request.GetValidCallId())
            {
            }

            public PendingCallKey(IInterprocessClientProxy client, long callId)
            {
                Client = client;
                CallId = callId;
            }

            public bool Equals(PendingCallKey other)
            {
                return other.CallId == CallId && Client.UniqueId == other.Client.UniqueId;
            }

            public override int GetHashCode()
            {
                return Client.UniqueId.GetHashCode() ^ CallId.GetHashCode();
            }

            public override string ToString()
            {
                return $"{CallId}@{Client.UniqueId}";
            }

            public override bool Equals(object obj) => (obj as PendingCallKey?)?.Equals(this) ?? false;
        }

        #endregion
    }
}
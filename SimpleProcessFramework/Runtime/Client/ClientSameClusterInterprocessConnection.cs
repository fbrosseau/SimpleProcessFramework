﻿using Spfx.Reflection;
using Spfx.Runtime.Client.Events;
using Spfx.Runtime.Common;
using Spfx.Runtime.Messages;
using Spfx.Runtime.Server;
using Spfx.Serialization;
using System;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client
{
    internal class ClientSameClusterInterprocessConnection : AbstractInterprocessConnection, IClientInterprocessConnection, IInterprocessClientChannel
    {
        private readonly IInternalMessageDispatcher m_localProcess;
        private readonly IInterprocessClientProxy m_proxyToThis;
        private readonly IBinarySerializer m_binarySerializer;
        private readonly IClientConnectionManager m_connectionManager;
        private readonly EventSubscriptionManager m_eventManager;

        public string UniqueId { get; }
        ProcessEndpointAddress IClientInterprocessConnection.Destination => ProcessEndpointAddress.RelativeClusterAddress;

        public IInterprocessClientProxy GetWrapperProxy() => m_proxyToThis;

        public ClientSameClusterInterprocessConnection(ITypeResolver typeResolver)
            : base(typeResolver)
        {
            m_localProcess = typeResolver.GetSingleton<IInternalMessageDispatcher>();
            UniqueId = m_localProcess.LocalProcessUniqueId + "/0";

            m_proxyToThis = new ConcreteClientProxy(this);
            m_binarySerializer = typeResolver.CreateSingleton<IBinarySerializer>();

            m_connectionManager = typeResolver.GetSingleton<IClientConnectionManager>();
            m_eventManager = new EventSubscriptionManager(typeResolver, this, ProcessEndpointAddress.RelativeClusterAddress);
        }

        protected override void OnDispose()
        {
            Logger.Info?.Trace($"{nameof(ClientSameClusterInterprocessConnection)}::OnDispose");
            base.OnDispose();
        }

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

        public ValueTask<ProcessEndpointMethodDescriptor> GetRemoteMethodDescriptor(ProcessEndpointAddress destination, ReflectedMethodInfo calledMethod)
        {
            throw new NotImplementedException();
        }

        public override void Initialize()
        {
            Logger.Info?.Trace("Initialize");
            m_connectionManager.RegisterClientChannel(this);
            BeginWrites();
        }

        void IInterprocessClientChannel.Initialize()
        {
            // TODO - Bad code
        }

        void IInterprocessClientChannel.SendMessageToClient(IInterprocessMessage msg)
        {
            msg = msg.Unwrap(m_binarySerializer);
            ProcessReceivedMessage(msg);
        }

        protected override void ProcessReceivedMessage(IInterprocessMessage msg)
        {
            Logger.Debug?.Trace("ProcessReceivedMessage: " + msg.GetTinySummaryString());
            if (!m_eventManager.ProcessIncomingMessage(msg))
                base.ProcessReceivedMessage(msg);
        }

        protected override ValueTask ExecuteWrite(IPendingOperation op)
        {
            var msg = op.Message;

            Logger.Info?.Trace("ExecuteWrite: " + msg.GetTinySummaryString());

            m_localProcess.ForwardOutgoingMessage(this, msg);
            return default;
        }
    }
}
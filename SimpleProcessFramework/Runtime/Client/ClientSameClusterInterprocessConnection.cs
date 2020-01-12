using Spfx.Reflection;
using Spfx.Runtime.Common;
using Spfx.Runtime.Messages;
using Spfx.Runtime.Server;
using Spfx.Serialization;
using Spfx.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client
{
    internal class ClientSameClusterInterprocessConnection : AbstractInterprocessConnection, IClientInterprocessConnection, IInterprocessClientChannel
    {
        private readonly IInternalMessageDispatcher m_localProcess;
        private readonly IInterprocessClientProxy m_proxyToThis;
        private readonly IBinarySerializer m_binarySerializer;
        private readonly IClientConnectionManager m_connectionManager;
        private readonly SimpleUniqueIdFactory<Action<EventRaisedMessage>> m_eventRegistrations = new SimpleUniqueIdFactory<Action<EventRaisedMessage>>();

        public string UniqueId { get; }

        public IInterprocessClientProxy GetWrapperProxy() => m_proxyToThis;
        
        public ClientSameClusterInterprocessConnection(ITypeResolver typeResolver)
            : base(typeResolver)
        {
            m_localProcess = typeResolver.GetSingleton<IInternalMessageDispatcher>();
            UniqueId = m_localProcess.LocalProcessUniqueId + "/0";

            m_proxyToThis = new ConcreteClientProxy(this);
            m_binarySerializer = typeResolver.CreateSingleton<IBinarySerializer>();

            m_connectionManager = typeResolver.GetSingleton<IClientConnectionManager>();
        }

        protected override void OnDispose()
        {
            Logger.Info?.Trace($"{nameof(ClientSameClusterInterprocessConnection)}::OnDispose");
            base.OnDispose();
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

            switch (msg)
            {
                case EventRaisedMessage eventMsg:
                    var handler = m_eventRegistrations.TryGetById(eventMsg.SubscriptionId);
                    handler?.Invoke(eventMsg);
                    break;
                default:
                    base.ProcessReceivedMessage(msg);
                    break;
            }
        }

        protected override ValueTask ExecuteWrite(PendingOperation op)
        {
            var msg = op.Message;

            Logger.Info?.Trace("ExecuteWrite: " + msg.GetTinySummaryString());

            m_localProcess.ForwardOutgoingMessage(this, msg);
            return default;
        }
    }
}
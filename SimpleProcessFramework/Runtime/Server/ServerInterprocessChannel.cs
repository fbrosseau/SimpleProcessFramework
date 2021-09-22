using Spfx.Reflection;
using Spfx.Runtime.Common;
using Spfx.Runtime.Messages;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server
{
    internal class ServerInterprocessChannel : StreamBasedInterprocessConnection, IInterprocessClientChannel
    {
        public string UniqueId { get; }

        private readonly Stream m_readStream;
        private readonly Stream m_writeStream;
        private readonly string m_localEndpoint;
        private readonly string m_remoteEndpoint;
        private readonly ITypeResolver m_typeResolver;
        private readonly IInterprocessClientProxy m_wrapperProxy;
        private readonly Action<EndpointLostEventArgs> m_processLostHandler;
        private readonly HashSet<string> m_subscribedProcesses;
        private IIncomingClientMessagesHandler m_messagesHandler;
        private static readonly SimpleUniqueIdFactory<IInterprocessClientChannel> s_idFactory = new SimpleUniqueIdFactory<IInterprocessClientChannel>();

        public ServerInterprocessChannel(ITypeResolver typeResolver, Stream readStream, Stream writeStream, string localEndpoint, string remoteEndpoint)
            : base(typeResolver, remoteEndpoint)
        {
            UniqueId = InterprocessConnectionId.ExternalConnectionIdPrefix + "/ " + s_idFactory.GetNextId(this) + "/" + remoteEndpoint;
            m_readStream = readStream;
            m_subscribedProcesses = new HashSet<string>(ProcessEndpointAddress.StringComparer);
            m_writeStream = writeStream;
            m_localEndpoint = localEndpoint;
            m_remoteEndpoint = remoteEndpoint;
            m_typeResolver = typeResolver;
            m_wrapperProxy = new ConcreteClientProxy(this);
            m_processLostHandler = OnProcessLost;
        }

        internal override Task<(Stream readStream, Stream writeStream)> ConnectStreamsAsync()
        {
            return Task.FromResult((m_readStream, m_writeStream));
        }

        void IInterprocessClientChannel.Initialize()
        {
            m_messagesHandler = m_typeResolver.GetSingleton<IIncomingClientMessagesHandler>();
            Initialize();
        }

        protected override void OnDispose()
        {
            Logger.Info?.Trace($"{nameof(ServerInterprocessChannel)}::OnDispose");

            string[] ids;
            lock (m_subscribedProcesses)
            {
                ids = m_subscribedProcesses.ToArray();
            }

            foreach (var subscribedProcess in ids)
            {
                m_messagesHandler.RemoveProcessLostHandler(subscribedProcess, m_processLostHandler);
            }

            base.OnDispose();
        }

        private void OnProcessLost(EndpointLostEventArgs e)
        {
            lock (m_subscribedProcesses)
            {
                m_subscribedProcesses.Remove(e.Address.ProcessId);
            }

            SendMessageToClient(new EndpointLostMessage
            {
                Endpoint = e.Address
            });
        }

        protected override void ProcessReceivedMessage(IInterprocessMessage msg)
        {
            Logger.Debug?.Trace($"ProcessReceivedMessage: {msg.GetTinySummaryString()}");

            if (!string.IsNullOrWhiteSpace(msg.Destination.ProcessId))
            {
                bool isNewProcessReference;
                lock (m_subscribedProcesses)
                {
                    isNewProcessReference = m_subscribedProcesses.Add(msg.Destination.ProcessId);
                }

                if (isNewProcessReference)
                {
                    m_messagesHandler.AddProcessLostHandler(msg.Destination.ProcessId, m_processLostHandler);
                }
            }

            switch (msg)
            {
                case WrappedInterprocessMessage wrapper:
                    m_messagesHandler.ForwardMessage(GetWrapperProxy(), wrapper);
                    break;
                default:
                    base.ProcessReceivedMessage(msg);
                    break;
            }
        }

        public void SendMessageToClient(IInterprocessMessage msg)
        {
            SerializeAndSendMessage(msg).FireAndForget();
        }

        public IInterprocessClientProxy GetWrapperProxy() => m_wrapperProxy;
    }
}
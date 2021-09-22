using Spfx.Runtime.Messages;
using System;

namespace Spfx.Runtime.Server
{
    internal class InternalMessageDispatcher : IIncomingClientMessagesHandler
    {
        private readonly IInternalProcessBroker m_processBroker;
        private readonly IClientConnectionManager m_externalConnectionsManager;

        public InternalMessageDispatcher(ProcessCluster cluster)
        {
            cluster.TypeResolver.RegisterSingleton<IIncomingClientMessagesHandler>(this);

            m_processBroker = cluster.TypeResolver.GetSingleton<IInternalProcessBroker>();
            m_externalConnectionsManager = cluster.TypeResolver.GetSingleton<IClientConnectionManager>();
        }

        void IIncomingClientMessagesHandler.AddProcessLostHandler(string processId, Action<EndpointLostEventArgs> onProcessLost)
        {
            m_processBroker.AddProcessLostHandler(processId, onProcessLost);
        }

        void IIncomingClientMessagesHandler.RemoveProcessLostHandler(string processId, Action<EndpointLostEventArgs> onProcessLost)
        {
            m_processBroker.RemoveProcessLostHandler(processId, onProcessLost);
        }

        public void ForwardMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage)
        {
            if (wrappedMessage.IsRequest || !source.IsExternalConnection)
            {
                m_processBroker.ForwardMessageToProcess(source, wrappedMessage);
            }
            else
            {
                var channel = m_externalConnectionsManager.GetClientChannel(source.UniqueId, mustExist: true);
                channel.SendMessageToClient(wrappedMessage);
            }
        }
    }
}

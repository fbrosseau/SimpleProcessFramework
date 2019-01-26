using System;
using System.Collections.Generic;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Utilities.Threading;

namespace SimpleProcessFramework.Runtime.Server
{
    internal class ClientConnectionManager : AsyncDestroyable, IClientConnectionManager, IClientRequestHandler
    {
        private readonly Dictionary<long, IInterprocessClientChannel> m_activeChannels = new Dictionary<long, IInterprocessClientChannel>();
        private readonly HashSet<IConnectionListener> m_listeners = new HashSet<IConnectionListener>();
        private readonly IInternalProcessBroker m_processManager;

        public ClientConnectionManager(IInternalProcessBroker processManager)
        {
            m_processManager = processManager;
        }

        public void AddListener(IConnectionListener listener)
        {
            lock (m_listeners)
            {
                if (!m_listeners.Add(listener))
                    throw new InvalidOperationException("This listener was added twice");
            }

            try
            {
                listener.Start(this);
            }
            catch
            {
                RemoveListener(listener);
            }
        }

        public void RemoveListener(IConnectionListener listener)
        {
            lock (m_listeners)
            {
                m_listeners.Remove(listener);
            }
        }

        void IClientConnectionManager.RegisterClientChannel(IInterprocessClientChannel cli)
        {
            lock (m_activeChannels)
            {
                m_activeChannels.Add(cli.UniqueId, cli);
            }

            cli.ConnectionLost += OnConnectionLost;
            cli.Initialize(this);
        }

        private void OnConnectionLost(object sender, EventArgs e)
        {
            lock (m_activeChannels)
            {
                m_activeChannels.Remove(((IInterprocessClientChannel)sender).UniqueId);
            }
        }

        void IClientRequestHandler.OnRequestReceived(IInterprocessClientChannel source, WrappedInterprocessMessage wrappedMessage)
        {
            m_processManager.ForwardMessage(source.GetWrapperProxy(), wrappedMessage);
        }

        public IInterprocessClientChannel GetClientChannel(long connectionId, bool mustExist)
        {
            IInterprocessClientChannel c;
            lock (m_activeChannels)
            {
                m_activeChannels.TryGetValue(connectionId, out c);
            }

            if (c is null && mustExist)
                throw new InvalidOperationException("This connection no longer exists");

            return c;
        }
    }
}
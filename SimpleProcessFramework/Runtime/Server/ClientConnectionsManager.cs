using System;
using System.Collections.Generic;
using SimpleProcessFramework.Runtime.Messages;

namespace SimpleProcessFramework.Runtime.Server
{
    internal class ClientConnectionManager : IClientConnectionManager, IClientRequestHandler
    {
        private readonly List<IInterprocessClientChannel> m_activeChannels = new List<IInterprocessClientChannel>();
        private readonly HashSet<IConnectionListener> m_listeners = new HashSet<IConnectionListener>();
        private readonly IInternalProcessManager m_processManager;

        public ClientConnectionManager(IInternalProcessManager processManager)
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
                listener.ConnectionReceived += OnConnectionReceived;
                listener.Start(this);
            }
            catch
            {
                RemoveListener(listener);
            }
        }

        public void RemoveListener(IConnectionListener listener)
        {
            listener.ConnectionReceived -= OnConnectionReceived;

            lock (m_listeners)
            {
                m_listeners.Remove(listener);
            }
        }

        private void OnConnectionReceived(object sender, IpcConnectionReceivedEventArgs e)
        {
            var cli = e.Client;
            lock (m_activeChannels)
            {
                m_activeChannels.Add(cli);
            }

            cli.ConnectionLost += OnConnectionLost;
            cli.Initialize(this);
        }

        private void OnConnectionLost(object sender, EventArgs e)
        {
            lock (m_activeChannels)
            {
                m_activeChannels.Remove((IInterprocessClientChannel)sender);
            }
        }

        void IClientRequestHandler.OnRequestReceived(IInterprocessClientChannel source, WrappedInterprocessMessage wrappedMessage)
        {
            m_processManager.ForwardMessage(source.GetWrapperProxy(), wrappedMessage);
        }
    }
}
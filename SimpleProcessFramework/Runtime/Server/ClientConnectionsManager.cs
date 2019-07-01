using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Spfx.Reflection;
using Spfx.Runtime.Messages;
using Spfx.Runtime.Server.Listeners;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server
{
    internal class ClientConnectionManager : AsyncDestroyable, IClientConnectionManager
    {
        private readonly Dictionary<string, IInterprocessClientChannel> m_activeChannels = new Dictionary<string, IInterprocessClientChannel>();
        private readonly HashSet<IConnectionListener> m_listeners = new HashSet<IConnectionListener>();
        private readonly ITypeResolver m_typeResolver;
        private IIncomingClientMessagesHandler m_messagesHandler;

        public ClientConnectionManager(ITypeResolver typeResolver)
        {
            m_typeResolver = typeResolver;
        }

        public void AddListener(IConnectionListener listener)
        {
            lock (m_listeners)
            {
                if (!m_listeners.Add(listener))
                    throw new InvalidOperationException("This listener was added twice");
            }

            if (m_messagesHandler is null)
                m_messagesHandler = m_typeResolver.GetSingleton<IIncomingClientMessagesHandler>();

            try
            {
                listener.Start(m_typeResolver);
            }
            catch
            {
                RemoveListener(listener);
            }
        }

        public List<EndPoint> GetListenEndpoints()
        {
            lock (m_listeners)
            {
                return m_listeners.OfType<IExternalConnectionsListener>().Select(l => l.ListenEndpoint).ToList();
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
            cli.Initialize();
        }

        private void OnConnectionLost(object sender, EventArgs e)
        {
            lock (m_activeChannels)
            {
                m_activeChannels.Remove(((IInterprocessClientChannel)sender).UniqueId);
            }
        }

        public IInterprocessClientChannel GetClientChannel(string connectionId, bool mustExist)
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
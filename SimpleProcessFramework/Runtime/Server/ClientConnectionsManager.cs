using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Server.Listeners;
using Spfx.Utilities;
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

        protected override async ValueTask OnTeardownAsync(CancellationToken ct = default)
        {
            var disposables = new List<IAsyncDestroyable>();

            lock (m_listeners)
            {
                disposables.AddRange(m_listeners);
                m_listeners.Clear();
            }

            lock(m_activeChannels)
            {
                disposables.AddRange(m_activeChannels.Values);
                m_activeChannels.Clear();
            }

            await TeardownAll(disposables, ct).ConfigureAwait(false);

            await base.OnTeardownAsync(ct).ConfigureAwait(false);
        }

        protected override void OnDispose()
        {
            var disposables = new List<IDisposable>();

            lock (m_listeners)
            {
                disposables.AddRange(m_listeners);
                m_listeners.Clear();
            }

            lock (m_activeChannels)
            {
                disposables.AddRange(m_activeChannels.Values);
                m_activeChannels.Clear();
            }

            foreach (var d in disposables)
            {
                d.Dispose();
            }

            base.OnDispose();
        }

        public void AddListener(IConnectionListener listener)
        {
            lock (m_listeners)
            {
                if (!m_listeners.Add(listener))
                    BadCodeAssert.ThrowInvalidOperation("This listener was added twice");
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
                throw new UnknownChannelException($"Channel {connectionId} does not exist");

            return c;
        }
    }
}
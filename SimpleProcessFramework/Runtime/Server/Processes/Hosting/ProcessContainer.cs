using Spfx.Utilities;
using Spfx.Runtime.Messages;
using Spfx.Utilities.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using Spfx.Runtime.Server.Processes.Ipc;
using Spfx.Reflection;
using Spfx.Diagnostics.Logging;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Linq;

namespace Spfx.Runtime.Server.Processes.Hosting
{
    internal class ProcessContainer : AsyncDestroyable, IIpcConnectorListener, IInternalMessageDispatcher
    {
        private IProcessInternal m_processCore;
        private ISubprocessConnector m_connector;
        private readonly List<SubprocessShutdownEvent> m_shutdownEvents = new List<SubprocessShutdownEvent>();
        protected ProcessSpawnPunchPayload InputPayload { get; private set; }
        private DisposableGCHandle m_gcHandleToThis;
        private ILogger m_logger = NullLogger.Logger;

        public string LocalProcessUniqueId => m_processCore.UniqueId;

        public ITypeResolver TypeResolver { get; private set; }

        private SubProcessConfiguration m_config;

        protected override void OnDispose()
        {
            m_logger.Info?.Trace("OnDispose");
            m_processCore?.Dispose();
            m_connector?.Dispose();
            base.OnDispose();
            //m_logger.Dispose();
            m_gcHandleToThis?.Dispose();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void Initialize(TextReader input)
        {
            m_gcHandleToThis = new DisposableGCHandle(this);

            InputPayload = ProcessSpawnPunchPayload.Deserialize(input);

            var typeResolverFactoryType = string.IsNullOrWhiteSpace(InputPayload.TypeResolverFactory)
                ? typeof(DefaultTypeResolverFactory)
                : Type.GetType(InputPayload.TypeResolverFactory, true);

            TypeResolver = DefaultTypeResolverFactory.CreateRootTypeResolver(typeResolverFactoryType);

            m_config = TypeResolver.CreateSingleton<SubProcessConfiguration>();

            m_logger = TypeResolver.GetLogger(GetType(), uniqueInstance: true, friendlyName: InputPayload.ProcessUniqueId);
            m_logger.Info?.Trace("Initialize");

            var timeout = TimeSpan.FromMilliseconds(InputPayload.HandshakeTimeout);
            if (timeout == TimeSpan.Zero)
                timeout = m_config.DefaultProcessInitTimeout;

            using var cts = new CancellationTokenSource(timeout);
            using var initializer = CreateInitializer();

            var ct = cts.Token;

            TypeResolver.RegisterSingleton<IIpcConnectorListener>(this);
            TypeResolver.RegisterSingleton<IInternalMessageDispatcher>(this);

            m_processCore = new ProcessCore(InputPayload.HostAuthority, InputPayload.ProcessUniqueId, TypeResolver);

            m_shutdownEvents.AddRange(initializer.GetHostShutdownEvents());
            m_shutdownEvents.Add(new SubprocessShutdownEvent(m_processCore.TerminateEvent, "Process Terminate"));

            m_logger.Info?.Trace("InitializeConnector");
            m_connector = initializer.CreateConnector(this);
            m_connector.InitializeAsync(ct).WithCancellation(ct).Wait(ct);

            initializer.OnInitSucceeded();

            m_logger.Info?.Trace("InitializeConnector completed");
        }

        protected virtual ProcessContainerInitializer CreateInitializer()
        {
            return ProcessContainerInitializer.Create(InputPayload, TypeResolver);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Run()
        {
            WaitForExit();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DebuggerStepThrough]
        private void WaitForExit()
        {
            try
            {
                var tasks = m_shutdownEvents.Select(e => e.WaitTask).ToArray();
                int res = Task.WaitAny(tasks);
                m_logger.Info?.Trace("Exiting: " + m_shutdownEvents[res].Description);
            }
            finally
            {
                try
                {
                    m_logger.Info?.Trace("Leaving WaitForExit");
                }
                catch
                {
                    // oh well.
                }
            }
        }

        void IIpcConnectorListener.OnMessageReceived(WrappedInterprocessMessage msg)
        {
            m_processCore.ProcessIncomingMessage(new ShallowConnectionProxy(m_connector, msg.SourceConnectionId), msg);
        }

        async void IIpcConnectorListener.OnTeardownRequestReceived()
        {
            try
            {
                m_logger.Info?.Trace("OnTeardownRequestReceived");
                await m_processCore.TeardownAsync().ConfigureAwait(false);
                await m_connector.TeardownAsync().ConfigureAwait(false);
            }
            finally
            {
                Dispose();
            }
        }

        Task IIpcConnectorListener.CompleteInitialization(CancellationToken ct)
        {
            m_logger.Info?.Trace("IIpcConnectorListener.CompleteInitialization");
            return m_processCore.InitializeAsync();
        }

        void IIpcConnectorListener.OnRemoteEndLost(string msg, Exception ex)
        {
            m_logger.Info?.Trace("IIpcConnectorListener.OnRemoteEndLost");
            Dispose();
        }

        void IInternalMessageDispatcher.ForwardOutgoingMessage(IInterprocessClientChannel source, IInterprocessMessage req)
        {
            Guard.ArgumentNotNull(source, nameof(source));
            Guard.ArgumentNotNull(req, nameof(req));

            m_connector.HandleMessage(source.UniqueId, req);
        }
    }
}
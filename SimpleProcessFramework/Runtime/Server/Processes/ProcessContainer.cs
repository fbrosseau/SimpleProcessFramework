using Microsoft.Win32.SafeHandles;
using Spfx.Utilities;
using Spfx.Io;
using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities.Threading;
using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using Spfx.Interfaces;
using System.Net.Sockets;
using Spfx.Runtime.Server.Processes.Ipc;
using Spfx.Reflection;
using Spfx.Utilities.Diagnostics;

namespace Spfx.Runtime.Server.Processes
{
    internal interface IMessageCallbackChannel
    {
        void HandleMessage(string connectionId, IInterprocessMessage msg);
        Task<IInterprocessClientChannel> GetClientInfo(string uniqueId);
    }

    internal interface ISubprocessConnector : IMessageCallbackChannel, IAsyncDestroyable
    {
    }

    internal class ProcessContainer : AsyncDestroyable, IIpcConnectorListener, IInternalMessageDispatcher
    {
        private IProcessInternal m_process;
        private ISubprocessConnector m_connector;
        private WaitHandle[] m_shutdownHandles;
        private ProcessSpawnPunchPayload m_inputPayload;
        private GCHandle m_gcHandleToThis;
        private ILogger m_logger;

        public string LocalProcessUniqueId => m_process.UniqueId;

        public ITypeResolver TypeResolver { get; private set; }

        protected override void OnDispose()
        {
            try
            {
                m_logger.Info?.Trace("OnDispose");
                m_process?.Dispose();
                m_connector?.Dispose();
                base.OnDispose();
                m_logger.Dispose();
            }
            finally
            {
                if (m_gcHandleToThis.IsAllocated)
                    m_gcHandleToThis.Free();
            }
        }

        internal void Initialize(TextReader input)
        {
            m_gcHandleToThis = GCHandle.Alloc(this);
            m_inputPayload = ProcessSpawnPunchPayload.Deserialize(input);

            var typeResolverFactoryType = string.IsNullOrWhiteSpace(m_inputPayload.TypeResolverFactory)
                ? typeof(DefaultTypeResolverFactory)
                : Type.GetType(m_inputPayload.TypeResolverFactory, true);

            TypeResolver = DefaultTypeResolverFactory.CreateRootTypeResolver(typeResolverFactoryType);
            TypeResolver.RegisterSingleton<IIpcConnectorListener>(this);
            TypeResolver.RegisterSingleton<IInternalMessageDispatcher>(this);
            m_logger = TypeResolver.GetLogger(GetType(), uniqueInstance: true);
            m_logger.Info?.Trace("Initialize");

            using (var disposeBag = new DisposeBag())
            {
                var shutdownHandles = new List<WaitHandle>();
                if (!string.IsNullOrWhiteSpace(m_inputPayload.ShutdownEvent))
                    shutdownHandles.Add(new Win32WaitHandle(ProcessSpawnPunchPayload.DeserializeHandleFromString(m_inputPayload.ShutdownEvent)));

                m_process = new Process2(m_inputPayload.HostAuthority, m_inputPayload.ProcessUniqueId, TypeResolver);
                shutdownHandles.Add(m_process.TerminateEvent);

                m_shutdownHandles = shutdownHandles.ToArray();

                disposeBag.ReleaseAll();
            }
        }

        internal void InitializeConnector()
        {
            m_logger.Info?.Trace("InitializeConnector");

            var timeout = TimeSpan.FromMilliseconds(m_inputPayload.HandshakeTimeout);
            if (timeout == TimeSpan.Zero)
                timeout = TimeSpan.FromSeconds(30);

            using (var disposeBag = new DisposeBag())
            using (var cts = new CancellationTokenSource(timeout))
            {
                var ct = cts.Token;
                Stream readStream, writeStream;
                if (m_inputPayload.ProcessKind != ProcessKind.Wsl)
                {
                    readStream = disposeBag.Add(new AnonymousPipeClientStream(PipeDirection.In, m_inputPayload.ReadPipe));
                    writeStream = disposeBag.Add(new AnonymousPipeClientStream(PipeDirection.Out, m_inputPayload.WritePipe));
                }
                else
                {
                    var sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    sock.Connect(SocketUtilities.CreateUnixEndpoint(m_inputPayload.ReadPipe));
                    readStream = writeStream = new NetworkStream(sock);
                }

                var streamReader = disposeBag.Add(LengthPrefixedStream.CreateReader(readStream, m_inputPayload.ProcessUniqueId + " - SubprocessRead"));
                var streamWriter = disposeBag.Add(LengthPrefixedStream.CreateWriter(writeStream, m_inputPayload.ProcessUniqueId + " - SubProcessWrite"));

                var connector = disposeBag.Add(new SubprocessIpcConnector(this, streamReader, streamWriter, TypeResolver));
                SetConnector(connector);
                connector.InitializeAsync(ct).WaitOrTimeout(TimeSpan.FromSeconds(30));
                disposeBag.ReleaseAll();
            }

            m_logger.Info?.Trace("InitializeConnector completed");
        }

        internal void SetConnector(ISubprocessConnector connector)
        {
            m_connector = connector;
        }

        internal void Run()
        {
            WaitHandle.WaitAny(m_shutdownHandles);
        }

        void IIpcConnectorListener.OnMessageReceived(WrappedInterprocessMessage msg)
        {
            m_process.ProcessIncomingMessage(new ShallowConnectionProxy(m_connector, msg.SourceConnectionId), msg);
        }

        async void IIpcConnectorListener.OnTeardownRequestReceived()
        {
            try
            {
                m_logger.Info?.Trace("OnTeardownRequestReceived");
                await m_process.TeardownAsync();
                await m_connector.TeardownAsync();
            }
            finally
            {
                Dispose();
            }
        }

        Task IIpcConnectorListener.CompleteInitialization()
        {
            m_logger.Info?.Trace("IIpcConnectorListener.CompleteInitialization");
            return m_process.InitializeAsync();
        }

        void IIpcConnectorListener.OnRemoteEndLost(string msg, Exception ex)
        {
            m_logger.Info?.Trace("IIpcConnectorListener.OnRemoteEndLost");
            Dispose();
        }

        void IInternalMessageDispatcher.ForwardOutgoingMessage(IInterprocessClientChannel source, IInterprocessMessage req, CancellationToken ct)
        {
            Guard.ArgumentNotNull(source, nameof(source));
            Guard.ArgumentNotNull(req, nameof(req));

            m_connector.HandleMessage(source.UniqueId, req);
        }
    }
}
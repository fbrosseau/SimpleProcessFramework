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

namespace Spfx.Runtime.Server.Processes
{
    internal class Win32WaitHandle : WaitHandle
    {
        public Win32WaitHandle(IntPtr h)
        {
            SafeWaitHandle = new SafeWaitHandle(h, true);
        }

        public Win32WaitHandle(SafeHandle h)
        {
            SafeWaitHandle = new SafeWaitHandle(h.DangerousGetHandle(), false);
        }
    }

    internal interface IMessageCallbackChannel
    {
        void SendBackMessage(string connectionId, IInterprocessMessage msg);
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

        public string LocalProcessUniqueId => m_process.UniqueId;

        protected override void OnDispose()
        {
            try
            {
                m_process?.Dispose();
                m_connector?.Dispose();
                base.OnDispose();
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
            Console.WriteLine("Successfully read input");

            var typeResolver = ProcessCluster.DefaultTypeResolver.CreateNewScope();
            typeResolver.RegisterSingleton<IIpcConnectorListener>(this);
            typeResolver.RegisterSingleton<IInternalMessageDispatcher>(this);

            using (var disposeBag = new DisposeBag())
            {
                var shutdownHandles = new List<WaitHandle>();
                if (!string.IsNullOrWhiteSpace(m_inputPayload.ShutdownEvent))
                    shutdownHandles.Add(new Win32WaitHandle(ProcessSpawnPunchPayload.DeserializeHandleFromString(m_inputPayload.ShutdownEvent)));

                m_process = new Process2(m_inputPayload.HostAuthority, m_inputPayload.ProcessUniqueId, typeResolver);
                shutdownHandles.Add(m_process.TerminateEvent);

                m_shutdownHandles = shutdownHandles.ToArray();

                disposeBag.ReleaseAll();
            }
        }

        internal void InitializeConnector()
        {
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
                    var sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                    sock.Connect(SocketUtilities.CreateUnixEndpoint(m_inputPayload.ReadPipe));
                    readStream = writeStream = new NetworkStream(sock);
                }

                var streamReader = disposeBag.Add(new SyncLengthPrefixedStreamReader(readStream, m_inputPayload.ProcessUniqueId + " - SlaveRead"));
                var streamWriter = disposeBag.Add(new SyncLengthPrefixedStreamWriter(writeStream, m_inputPayload.ProcessUniqueId + " - SlaveWrite"));

                var connector = disposeBag.Add(new SubprocessIpcConnector(this, streamReader, streamWriter, new DefaultBinarySerializer()));
                SetConnector(connector);
                connector.InitializeAsync(ct).WaitOrTimeout(TimeSpan.FromSeconds(30));
                disposeBag.ReleaseAll();
            }
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
            return m_process.InitializeAsync();
        }

        void IInternalMessageDispatcher.ForwardOutgoingMessage(IInterprocessClientChannel source, IInterprocessMessage req, CancellationToken ct)
        {
            Guard.ArgumentNotNull(source, nameof(source));
            Guard.ArgumentNotNull(req, nameof(req));

            m_connector.SendBackMessage(source.UniqueId, req);
        }
    }

    internal class ShallowConnectionProxy : IInterprocessClientProxy
    {
        public string UniqueId { get; }

        private readonly IMessageCallbackChannel m_owner;

        public ShallowConnectionProxy(IMessageCallbackChannel owner, string sourceId)
        {
            Guard.ArgumentNotNull(owner, nameof(owner));
            UniqueId = sourceId;
            m_owner = owner;
        }

        public Task<IInterprocessClientChannel> GetClientInfo()
        {
            return m_owner.GetClientInfo(UniqueId);
        }

        public void SendMessage(IInterprocessMessage msg)
        {
            m_owner.SendBackMessage(UniqueId, msg);
        }
    }
}
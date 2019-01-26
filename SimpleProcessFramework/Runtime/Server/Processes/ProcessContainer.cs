using Microsoft.Win32.SafeHandles;
using SimpleProcessFramework.Utilities;
using SimpleProcessFramework.Io;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Serialization;
using SimpleProcessFramework.Utilities.Threading;
using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Server.Processes
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

    internal class ProcessContainer : AsyncDestroyable, IIpcConnectorListener, IInternalMessageDispatcher
    {
        private IProcessInternal m_process;
        private SubprocessIpcConnector m_connector;
        private WaitHandle[] m_shutdownHandles;

        protected override void OnDispose()
        {
            m_process?.Dispose();
            m_connector?.Dispose();
            base.OnDispose();
        }

        internal void Initialize()
        {
            var inputPayload = ProcessSpawnPunchPayload.Deserialize(Console.In);

            var typeResolver = ProcessCluster.DefaultTypeResolver.CreateNewScope();
            typeResolver.RegisterSingleton<IIpcConnectorListener>(this);
            typeResolver.RegisterSingleton<IInternalMessageDispatcher>(this);

            using (var disposeBag = new DisposeBag())
            {
                m_shutdownHandles = new[] { new Win32WaitHandle(ProcessSpawnPunchPayload.DeserializeHandleFromString(inputPayload.ShutdownEvent)) };

                var readStream = disposeBag.Add(new AnonymousPipeClientStream(PipeDirection.In, inputPayload.ReadPipe));
                var writeStream = disposeBag.Add(new AnonymousPipeClientStream(PipeDirection.Out, inputPayload.WritePipe));

                var streamReader = disposeBag.Add(new SyncLengthPrefixedStreamReader(readStream, inputPayload.ProcessUniqueId + " - Read"));
                var streamWriter = disposeBag.Add(new SyncLengthPrefixedStreamWriter(writeStream, inputPayload.ProcessUniqueId + " - Write"));
                m_process = new Process2(inputPayload.HostAuthority, inputPayload.ProcessUniqueId, typeResolver);

                m_connector = disposeBag.Add(new SubprocessIpcConnector(this, streamReader, streamWriter, new DefaultBinarySerializer()));
                m_connector.InitializeAsync().WaitOrTimeout(TimeSpan.FromSeconds(30));

                disposeBag.ReleaseAll();
            }
        }

        internal void Run()
        {
            WaitHandle.WaitAny(m_shutdownHandles);
        }

        private class ShallowConnectionProxy : IInterprocessClientProxy
        {
            public long UniqueId { get; }

            private readonly ProcessContainer m_owner;

            public ShallowConnectionProxy(ProcessContainer owner, long sourceId)
            {
                UniqueId = sourceId;
                m_owner = owner;
            }

            public Task<IInterprocessClientChannel> GetClientInfo()
            {
                throw new NotImplementedException();
            }

            public void SendMessage(IInterprocessMessage msg)
            {
                m_owner.SerializeAndSendMessage(UniqueId, msg);
            }
        }

        private void SerializeAndSendMessage(long connectionId, IInterprocessMessage msg)
        {
            m_connector.SendBackMessage(connectionId, msg);
        }

        void IIpcConnectorListener.OnMessageReceived(WrappedInterprocessMessage msg)
        {
            m_process.ProcessIncomingMessage(new ShallowConnectionProxy(this, msg.SourceConnectionId), msg);
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

            var sourceProxy = source.GetWrapperProxy();
            m_connector.ForwardMessage(req);
        }
    }
}

using Microsoft.Win32.SafeHandles;
using SimpleProcessFramework.Io;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Serialization;
using SimpleProcessFramework.Utilities;
using System;
using System.IO.Pipes;
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

        public static Win32WaitHandle FromString(string str)
        {
            return new Win32WaitHandle(new IntPtr(unchecked((long)ulong.Parse(str))));
        }
    }

    internal class ProcessContainer : IDisposable, IIpcConnectorListener
    {
        private IProcessInternal m_process;
        private SubprocessIpcConnector m_connector;
        private WaitHandle[] m_shutdownHandles;

        public void Dispose()
        {
            m_process?.Dispose();
            m_connector?.Dispose();
        }

        internal void Initialize()
        {
            var inputPayload = ProcessSpawnPunchPayload.Deserialize(Console.In);

            using (var disposeBag = new DisposeBag())
            {
              //  m_shutdownHandles = new[] { Win32WaitHandle.FromString(inputPayload.ShutdownEvent) };
                var readStream = disposeBag.Add(new AnonymousPipeClientStream(PipeDirection.In, inputPayload.ReadPipe));
                var writeStream = disposeBag.Add(new AnonymousPipeClientStream(PipeDirection.Out, inputPayload.WritePipe));

                var streamReader = disposeBag.Add(new SyncLengthPrefixedStreamReader(readStream, inputPayload.ProcessUniqueId + " - Read"));
                var streamWriter = disposeBag.Add(new SyncLengthPrefixedStreamWriter(writeStream, inputPayload.ProcessUniqueId + " - Write"));
                m_process = new Process2(inputPayload.HostAuthority, inputPayload.ProcessUniqueId, ProcessCluster.DefaultTypeResolver);

                m_connector = disposeBag.Add(new SubprocessIpcConnector(this, streamReader, streamWriter, new DefaultBinarySerializer()));
                m_connector.InitializeAsync().WaitOrTimeout(TimeSpan.FromSeconds(30));

                disposeBag.ReleaseAll();
            }
        }

        internal void Run()
        {
            Thread.Sleep(-1);
            //WaitHandle.WaitAny(m_shutdownHandles);
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
            m_process.HandleMessage(new ShallowConnectionProxy(this, msg.SourceConnectionId), msg);
        }

        void IIpcConnectorListener.OnTeardownReceived()
        {
        }

        Task IIpcConnectorListener.CompleteInitialization()
        {
            return m_process.InitializeAsync();
        }
    }
}

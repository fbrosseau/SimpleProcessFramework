using System;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server.Processes
{
    internal abstract class GenericProcessHandle : AsyncDestroyable, IProcessHandle, IMessageCallbackChannel
    {
        public ProcessKind ProcessKind => ProcessCreationInfo.ProcessKind;
        public string ProcessUniqueId => ProcessCreationInfo.ProcessUniqueId;
        public ProcessCreationInfo ProcessCreationInfo { get; }
        protected ProcessClusterConfiguration Config { get; }
        public ProcessInformation ProcessInfo { get; private set; }
        private readonly IInternalProcessBroker m_processBroker;
        private readonly AsyncManualResetEvent m_initEvent = new AsyncManualResetEvent();

        protected IBinarySerializer BinarySerializer { get; }

        protected GenericProcessHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
        {
            ProcessCreationInfo = info;
            Config = typeResolver.GetSingleton<ProcessClusterConfiguration>();
            BinarySerializer = typeResolver.GetSingleton<IBinarySerializer>();
            m_processBroker = typeResolver.GetSingleton<IInternalProcessBroker>();
        }

        public async Task CreateProcess(ProcessSpawnPunchPayload punchPayload)
        {
            ProcessInfo = await CreateActualProcessAsync(punchPayload);
            m_initEvent.Set();
        }

        protected override void OnDispose()
        {
            m_initEvent.Dispose();
            base.OnDispose();
        }

        protected abstract Task<ProcessInformation> CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload);

        void IProcessHandle.HandleMessage(string connectionId, WrappedInterprocessMessage wrappedMessage)
        {
            _ = PrepareTransferToRemote(connectionId, wrappedMessage);
        }

        public void HandleMessage(string connectionId, IInterprocessMessage msg)
        {
            var wrapped = WrappedInterprocessMessage.Wrap(msg, BinarySerializer);
            wrapped.SourceConnectionId = connectionId;
            _ = PrepareTransferToRemote(connectionId, wrapped);
        }

        private async ValueTask PrepareTransferToRemote(string connectionId, WrappedInterprocessMessage wrappedMessage)
        {
            try
            {
                await WaitForInitializationComplete();
                TransferMessageToRemote(connectionId, wrappedMessage);
            }
            catch(Exception ex)
            {
                OnDeliveryFailed(wrappedMessage, ex);
            }
        }

        protected abstract void TransferMessageToRemote(string sourceConnectionId, WrappedInterprocessMessage wrappedMessage);

        protected void OnMessageReceivedFromProcess(WrappedInterprocessMessage msg)
        {
            DispatchMessageBackToMaster(msg);
        }

        protected void OnDeliveryFailed(WrappedInterprocessMessage wrappedMessage, Exception ex)
        {
            throw new NotImplementedException();
        }

        private void DispatchMessageBackToMaster(WrappedInterprocessMessage msg)
        {
            m_processBroker.ForwardMessage(new ShallowConnectionProxy(this, msg.SourceConnectionId), msg);
        }

        Task<IInterprocessClientChannel> IMessageCallbackChannel.GetClientInfo(string uniqueId)
        {
            throw new NotImplementedException();
        }

        public ValueTask WaitForInitializationComplete()
        {
            return m_initEvent.WaitAsync();
        }
    }
}
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
        private readonly IInternalProcessBroker m_processBroker;
        public ProcessInformation ProcessInfo { get; private set; }

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
        }

        protected abstract Task<ProcessInformation> CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload);
        protected abstract override Task OnTeardownAsync(CancellationToken ct);
        protected abstract override void OnDispose();

        void IProcessHandle.HandleMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage)
        {
            HandleMessage(source.UniqueId, wrappedMessage);
        }
        
        void IProcessHandle.ProcessIncomingRequest(IInterprocessClientProxy source, IInterprocessMessage msg)
        {
            SendBackMessage(source.UniqueId, msg);
        }

        public void SendBackMessage(string connectionId, IInterprocessMessage msg)
        {
            var wrapped = WrappedInterprocessMessage.Wrap(msg, BinarySerializer);
            wrapped.SourceConnectionId = connectionId;
            HandleMessage(connectionId, wrapped);
        }

        protected abstract void HandleMessage(string sourceConnectionId, WrappedInterprocessMessage wrappedMessage);

        protected void OnMessageReceivedFromProcess(WrappedInterprocessMessage msg)
        {
            m_processBroker.ForwardMessage(new ShallowConnectionProxy(this, msg.SourceConnectionId), msg);
        }

        Task<IInterprocessClientChannel> IMessageCallbackChannel.GetClientInfo(string uniqueId)
        {
            throw new NotImplementedException();
        }
    }
}
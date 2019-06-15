using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Messages;
using Spfx.Runtime.Server.Processes.Ipc;

namespace Spfx.Runtime.Server.Processes
{
    internal class SameProcessFakeHandle : GenericProcessHandle
    {
        private readonly ProcessContainer m_processContainer;
        private readonly IIpcConnectorListener m_rawProcessContainer;

        public SameProcessFakeHandle(ProcessCreationInfo info, ITypeResolver typeResolver) 
            : base(info, typeResolver)
        {
            m_processContainer = new ProcessContainer();
            m_rawProcessContainer = m_processContainer;
        }

        protected override async Task<ProcessInformation> CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload)
        {
            m_processContainer.Initialize(new StringReader(punchPayload.SerializeToString()));
            m_processContainer.SetConnector(new FakeSubprocessConnector(this));

            await m_rawProcessContainer.CompleteInitialization();

            return new ProcessInformation(ProcessUniqueId, Process.GetCurrentProcess().Id, ProcessCreationInfo.ProcessKind);
        }

        private class FakeSubprocessConnector : ISubprocessConnector
        {
            private readonly SameProcessFakeHandle m_owner;
            private readonly IProcessHandle m_rawOwner;

            public FakeSubprocessConnector(SameProcessFakeHandle owner)
            {
                m_owner = owner;
                m_rawOwner = owner;
            }

            public void Dispose()
            {
            }

            public Task<IInterprocessClientChannel> GetClientInfo(string uniqueId)
            {
                throw new System.NotImplementedException();
            }

            public void SendBackMessage(string connectionId, IInterprocessMessage msg)
            {
                var wrapped = WrappedInterprocessMessage.Wrap(msg, m_owner.BinarySerializer);
                wrapped.SourceConnectionId = connectionId;
                m_owner.OnMessageReceivedFromProcess(wrapped);
            }

            public Task TeardownAsync(CancellationToken ct = default)
            {
                return Task.CompletedTask;
            }
        }

        protected override void HandleMessage(string sourceConnectionId, WrappedInterprocessMessage wrappedMessage)
        {
            m_rawProcessContainer.OnMessageReceived(wrappedMessage);
        }

        protected override void OnDispose()
        {
            m_processContainer.Dispose();
        }

        protected override Task OnTeardownAsync(CancellationToken ct)
        {
            return m_processContainer.TeardownAsync(ct);
        }
    }    
}

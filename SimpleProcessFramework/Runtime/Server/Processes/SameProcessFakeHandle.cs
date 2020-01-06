using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Messages;
using Spfx.Runtime.Server.Processes.Hosting;
using Spfx.Runtime.Server.Processes.Ipc;
using Spfx.Utilities;

namespace Spfx.Runtime.Server.Processes
{
    internal class SameProcessFakeHandle : GenericProcessHandle
    {
        private readonly ProcessContainer m_processContainer;
        private readonly IIpcConnectorListener m_rawProcessContainer;

        public SameProcessFakeHandle(ProcessCreationInfo info, ITypeResolver typeResolver) 
            : base(info, typeResolver)
        {
            m_processContainer = new FakeProcessContainer(this);
            m_rawProcessContainer = m_processContainer;
        }

        protected override async Task<ProcessInformation> CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload, CancellationToken ct)
        {
            m_processContainer.Initialize(new StringReader(punchPayload.SerializeToString()));
            await m_rawProcessContainer.CompleteInitialization(ct);
            return new ProcessInformation(ProcessUniqueId, ProcessUtilities.CurrentProcessId, ProcessCreationInfo.TargetFramework);
        }

        protected override void TransferMessageToRemote(string sourceConnectionId, WrappedInterprocessMessage wrappedMessage)
        {
            wrappedMessage.SourceConnectionId = sourceConnectionId;
            m_rawProcessContainer.OnMessageReceived(wrappedMessage);
        }

        protected override void OnDispose()
        {
            m_processContainer.Dispose();
        }

        protected override ValueTask OnTeardownAsync(CancellationToken ct)
        {
            return m_processContainer.TeardownAsync(ct);
        }

        private class FakeProcessContainer : ProcessContainer
        {
            private readonly SameProcessFakeHandle m_parentHandle;

            public FakeProcessContainer(SameProcessFakeHandle parentHandle)
            {
                m_parentHandle = parentHandle;
            }

            protected override ProcessContainerInitializer CreateInitializer()
            {
                return new FakeProcessContainerInitializer(InputPayload, TypeResolver, m_parentHandle);
            }
        }

        private class FakeProcessContainerInitializer : ProcessContainerInitializer
        {
            private readonly SameProcessFakeHandle m_parentHandle;

            public FakeProcessContainerInitializer(ProcessSpawnPunchPayload payload, ITypeResolver typeResolver, SameProcessFakeHandle parentHandle)
                : base(payload, typeResolver)
            {
                m_parentHandle = parentHandle;
            }

            internal override ISubprocessConnector CreateConnector(ProcessContainer owner)
            {
                return new FakeSubprocessConnector(m_parentHandle);
            }

            internal override IEnumerable<Task> GetShutdownEvents()
                => Enumerable.Empty<Task>();
        }

        private class FakeSubprocessConnector : ISubprocessConnector
        {
            private readonly SameProcessFakeHandle m_owner;

            public FakeSubprocessConnector(SameProcessFakeHandle owner)
            {
                m_owner = owner;
            }

            public void Dispose()
            {
            }

            public ValueTask<IInterprocessClientChannel> GetClientInfo(string uniqueId)
            {
                throw new System.NotImplementedException();
            }

            public void HandleMessage(string connectionId, IInterprocessMessage msg)
            {
                var wrapped = WrappedInterprocessMessage.Wrap(msg, m_owner.BinarySerializer);
                wrapped.SourceConnectionId = connectionId;
                m_owner.OnMessageReceivedFromProcess(wrapped);
            }

            public Task InitializeAsync(CancellationToken ct)
                => Task.CompletedTask;
            public ValueTask TeardownAsync(CancellationToken ct = default)
                => default;
        }
    }
}

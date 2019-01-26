using System.Threading;
using System.Threading.Tasks;
using SimpleProcessFramework.Interfaces;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Serialization;
using SimpleProcessFramework.Utilities;
using SimpleProcessFramework.Utilities.Threading;

namespace SimpleProcessFramework.Runtime.Server.Processes
{
    internal abstract class GenericProcessHandle : AsyncDestroyable, IProcessHandle
    {
        public ProcessKind ProcessKind => ProcessCreationInfo.ProcessKind;
        public string ProcessUniqueId => ProcessCreationInfo.ProcessUniqueId;
        public ProcessCreationInfo ProcessCreationInfo { get; }
        protected ProcessClusterConfiguration Config { get; }
        private readonly SimpleUniqueIdFactory<TaskCompletionSource<object>> m_pendingRequests = new SimpleUniqueIdFactory<TaskCompletionSource<object>>();

        private readonly IBinarySerializer m_binarySerializer;

        protected GenericProcessHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
        {
            ProcessCreationInfo = info;
            Config = typeResolver.GetSingleton<ProcessClusterConfiguration>();
            m_binarySerializer = typeResolver.GetSingleton<IBinarySerializer>();
        }

        public abstract Task CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload);
        protected abstract override Task OnTeardownAsync(CancellationToken ct);
        protected abstract override void OnDispose();

        void IProcessHandle.HandleMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage)
        {
            HandleMessage(source, wrappedMessage);
        }
        
        Task<object> IProcessHandle.ProcessIncomingRequest(IInterprocessClientProxy source, IInterprocessMessage msg)
        {
            Task<object> completion = BoxHelper.GetDefaultSuccessTask<object>();

            if (msg is IInterprocessRequest req && req.ExpectResponse)
            {
                var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                req.CallId = m_pendingRequests.GetNextId(tcs);
                completion = tcs.Task;
            }

            var wrapped = WrappedInterprocessMessage.Wrap(msg, m_binarySerializer);

            HandleMessage(source, wrapped);
            return completion;
        }

        protected abstract void HandleMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage);
    }
}
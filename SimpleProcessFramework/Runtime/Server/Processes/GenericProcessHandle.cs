using System;
using System.Threading.Tasks;
using SimpleProcessFramework.Interfaces;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Messages;

namespace SimpleProcessFramework.Runtime.Server.Processes
{
    internal abstract class GenericProcessHandle : IProcessHandle
    {
        public ProcessKind ProcessKind => ProcessCreationInfo.ProcessKind;
        public string ProcessUniqueId => ProcessCreationInfo.ProcessUniqueId;
        public ProcessCreationInfo ProcessCreationInfo { get; }
        protected ProcessClusterConfiguration Config { get; }

        protected GenericProcessHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
        {
            ProcessCreationInfo = info;
            Config = typeResolver.GetSingleton<ProcessClusterConfiguration>();
        }

        public abstract Task CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload);
        public abstract Task DestroyAsync();
        public abstract void Dispose();

        void IProcessHandle.HandleMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage)
        {
            HandleMessage(source, wrappedMessage);
        }

        protected abstract void HandleMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage);
    }
}
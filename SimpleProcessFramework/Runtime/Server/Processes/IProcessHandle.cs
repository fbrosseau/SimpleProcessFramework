using System;
using System.Threading.Tasks;
using SimpleProcessFramework.Interfaces;
using SimpleProcessFramework.Runtime.Messages;

namespace SimpleProcessFramework.Runtime.Server.Processes
{
    public interface IProcessHandle : IDisposable
    {
        string ProcessUniqueId { get; }
        ProcessKind ProcessKind { get; }

        void HandleMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage);
        Task DestroyAsync();
        Task CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload);
    }
}
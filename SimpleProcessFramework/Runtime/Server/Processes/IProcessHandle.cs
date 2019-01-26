using System.Threading.Tasks;
using SimpleProcessFramework.Interfaces;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Utilities.Threading;

namespace SimpleProcessFramework.Runtime.Server.Processes
{
    public interface IProcessHandle : IAsyncDestroyable
    {
        string ProcessUniqueId { get; }
        ProcessKind ProcessKind { get; }

        void HandleMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage);
        Task<object> ProcessIncomingRequest(IInterprocessClientProxy source, IInterprocessMessage req);

        Task CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload);
    }
}
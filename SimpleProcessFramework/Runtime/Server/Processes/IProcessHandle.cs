using System.Threading.Tasks;
using Spfx.Interfaces;
using Spfx.Runtime.Messages;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server.Processes
{
    public interface IProcessHandle : IAsyncDestroyable
    {
        string ProcessUniqueId { get; }
        ProcessKind ProcessKind { get; }

        void HandleMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage);
        void ProcessIncomingRequest(IInterprocessClientProxy source, IInterprocessMessage req);

        Task CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload);
    }
}
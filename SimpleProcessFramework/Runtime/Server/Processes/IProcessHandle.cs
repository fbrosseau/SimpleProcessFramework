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
        ProcessInformation ProcessInfo { get; }

        void HandleMessage(string connectionId, WrappedInterprocessMessage wrappedMessage);
        void HandleMessage(string connectionId, IInterprocessMessage wrappedMessage);

        Task CreateProcess(ProcessSpawnPunchPayload punchPayload);
        ValueTask WaitForInitializationComplete();
    }
}
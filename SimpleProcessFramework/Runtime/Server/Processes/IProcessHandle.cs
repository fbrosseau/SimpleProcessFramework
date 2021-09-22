using Spfx.Interfaces;
using Spfx.Runtime.Messages;
using Spfx.Utilities.Threading;
using System;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes
{
    public interface IProcessHandle : IAsyncDestroyable
    {
        string ProcessUniqueId { get; }
        TargetFramework TargetFramework { get; }
        ProcessInformation ProcessInfo { get; }

        void HandleMessage(string connectionId, WrappedInterprocessMessage wrappedMessage);
        void HandleMessage(string connectionId, IInterprocessMessage wrappedMessage);

        Task CreateProcess();
        ValueTask WaitForInitializationComplete();

        event EventHandler ProcessExited;
    }
}
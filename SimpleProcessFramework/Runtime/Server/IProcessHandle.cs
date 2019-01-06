using System;
using System.Threading.Tasks;
using SimpleProcessFramework.Runtime.Messages;

namespace SimpleProcessFramework.Runtime.Server
{
    public interface IProcessHandle : IDisposable
    {
        void HandleMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage);
        Task DestroyAsync();
        Task CreateActualProcessAsync();
    }
}
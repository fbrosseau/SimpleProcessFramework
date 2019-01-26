using SimpleProcessFramework.Utilities.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Server
{
    internal interface IProcessEndpointHandler : IAsyncDestroyable
    {
        void HandleMessage(IInterprocessRequestContext req);
        void CompleteCall(IInterprocessRequestContext req);
        Task InitializeAsync(IProcess process2);
    }
}
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Server
{
    internal interface IProcessEndpointHandler
    {
        void HandleMessage(IInterprocessRequestContext req);
        void CompleteCall(IInterprocessRequestContext req);
        Task InitializeAsync(IProcess process2);
    }
}
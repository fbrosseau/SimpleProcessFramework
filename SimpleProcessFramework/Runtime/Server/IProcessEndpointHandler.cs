using Spfx.Utilities.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server
{
    internal interface IProcessEndpointHandler : IAsyncDestroyable
    {
        object ImplementationObject { get; }

        void HandleMessage(IInterprocessRequestContext req);
        void CompleteCall(IInterprocessRequestContext req);
        ValueTask InitializeAsync();
    }
}
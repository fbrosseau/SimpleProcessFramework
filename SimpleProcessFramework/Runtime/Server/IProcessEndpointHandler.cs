using Spfx.Utilities.Threading;
using System;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server
{
    internal interface IProcessEndpointHandler : IAsyncDestroyable
    {
        object ImplementationObject { get; }
        string EndpointId { get; }

        void HandleMessage(IInterprocessRequestContext req);
        void CompleteCall(IInterprocessRequestContext req);
        ValueTask InitializeAsync();
    }
}
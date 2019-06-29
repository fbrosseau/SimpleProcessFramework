using Spfx.Utilities.Threading;
using System;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server
{
    public interface IProcessEndpoint : IAsyncDestroyable
    {
        Task InitializeAsync(IProcess parentProcess);

        bool FilterMessage(IInterprocessRequestContext request);
    }

    public class AbstractProcessEndpoint : AsyncDestroyable, IProcessEndpoint
    {
        public IProcess ParentProcess { get; private set; }

        Task IProcessEndpoint.InitializeAsync(IProcess parentProcess)
        {
            ParentProcess = parentProcess;
            return InitializeAsync();
        }

        protected virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        protected virtual bool FilterMessage(IInterprocessRequestContext request) => true;

        bool IProcessEndpoint.FilterMessage(IInterprocessRequestContext request)
        {
            return FilterMessage(request);
        }
    }
}
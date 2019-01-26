using SimpleProcessFramework.Utilities.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Server
{
    public interface IProcessEndpoint : IAsyncDestroyable
    {
        Task InitializeAsync(IProcess parentProcess);
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
    }
}
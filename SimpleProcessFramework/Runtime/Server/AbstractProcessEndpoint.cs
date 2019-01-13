using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Server
{
    public interface IProcessEndpoint : IDisposable
    {
        Task InitializeAsync(IProcess parentProcess);
        Task TeardownAsync(CancellationToken ct = default);
    }

    public class AbstractProcessEndpoint : IProcessEndpoint
    {
        public IProcess ParentProcess { get; private set; }

        public virtual void Dispose()
        {
        }

        Task IProcessEndpoint.InitializeAsync(IProcess parentProcess)
        {
            ParentProcess = parentProcess;
            return InitializeAsync();
        }

        protected virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task TeardownAsync(CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }
}
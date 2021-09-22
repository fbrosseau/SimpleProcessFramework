using Spfx.Interfaces;
using System;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server
{
    internal class EndpointBroker : AbstractProcessEndpoint, IEndpointBroker
    {
        public event EventHandler<EndpointEventArgs> EndpointOpened { add { } remove { } }
        public event EventHandler<EndpointEventArgs> EndpointClosed { add { } remove { } }

        public async Task<ProcessCreationResults> CreateEndpoint(EndpointCreationRequest req)
        {
            var res = await ParentProcess.InitializeEndpointAsync(req.EndpointId, req.EndpointType.ResolvedType, req.ImplementationType.ResolvedType, req.Options).ConfigureAwait(false);
            return res.Result;
        }

        public Task<bool> DestroyEndpoint(string uniqueId)
        {
            return ParentProcess.DestroyEndpoint(uniqueId).AsTask();
        }

        public Task<ProcessCreationInfo> GetProcessCreationInfo()
        {
            return Task.FromResult(ParentProcess.ProcessCreationInfo);
        }
    }
}
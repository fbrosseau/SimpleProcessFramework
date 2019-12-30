using System;
using System.Threading.Tasks;
using Spfx.Interfaces;

namespace Spfx.Runtime.Server
{
    internal class EndpointBroker : AbstractProcessEndpoint, IEndpointBroker
    {
        public event EventHandler<EndpointEventArgs> EndpointOpened { add { } remove { } }
        public event EventHandler<EndpointEventArgs> EndpointClosed { add { } remove { } }

        public Task<ProcessCreationOutcome> CreateEndpoint(EndpointCreationRequest req)
        {
            return ParentProcess.InitializeEndpointAsync(req.EndpointId, req.EndpointType.ResolvedType, req.ImplementationType.ResolvedType, req.Options);
        }

        public Task<bool> DestroyEndpoint(string uniqueId)
        {
            return ParentProcess.DestroyEndpoint(uniqueId);
        }

        public Task<ProcessCreationInfo> GetProcessCreationInfo()
        {
            return Task.FromResult(ParentProcess.ProcessCreationInfo);
        }
    }
}
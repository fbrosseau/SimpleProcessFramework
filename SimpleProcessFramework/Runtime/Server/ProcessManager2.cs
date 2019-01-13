using System.Threading.Tasks;
using SimpleProcessFramework.Interfaces;
using SimpleProcessFramework.Reflection;

namespace SimpleProcessFramework.Runtime.Server
{
    internal class EndpointBroker : AbstractProcessEndpoint, IEndpointBroker
    {
        public Task<bool> CreateEndpoint(string uniqueId, ReflectedTypeInfo endpointType, ReflectedTypeInfo implementationType, bool failIfExists)
        {
            return ParentProcess.InitializeEndpointAsync(uniqueId, endpointType.ResolvedType, implementationType.ResolvedType, failIfExists);
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
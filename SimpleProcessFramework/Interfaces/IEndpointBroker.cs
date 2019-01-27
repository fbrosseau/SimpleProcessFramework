using Spfx.Reflection;
using System.Threading.Tasks;

namespace Spfx.Interfaces
{
    public interface IEndpointBroker
    {
        Task<ProcessCreationInfo> GetProcessCreationInfo();

        Task<bool> CreateEndpoint(string uniqueId, ReflectedTypeInfo endpointType, ReflectedTypeInfo implementationType, bool failIfExists = true);
        Task<bool> DestroyEndpoint(string uniqueId);
    }
}

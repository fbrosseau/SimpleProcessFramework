using Spfx.Reflection;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Spfx.Interfaces
{
    [DataContract]
    public class EndpointEventArgs : EventArgs
    {
        [DataMember]
        public string EndpointId { get; private set; }
        [DataMember]
        public string ProcessId { get; private set; }

        public EndpointEventArgs(string processId, string endpointId)
        {
            ProcessId = processId;
            EndpointId = endpointId;
        }
    }

    public interface IEndpointBroker
    {
        event EventHandler<EndpointEventArgs> EndpointOpened;
        event EventHandler<EndpointEventArgs> EndpointClosed;

        Task<ProcessCreationInfo> GetProcessCreationInfo();

        Task<ProcessCreationResults> CreateEndpoint(EndpointCreationRequest req);
        Task<bool> DestroyEndpoint(string uniqueId);
    }

    public static class EndpointBrokerExtensions
    {
        public static Task<ProcessCreationResults> CreateEndpoint(this IEndpointBroker broker, string uniqueId, ReflectedTypeInfo endpointType, ReflectedTypeInfo implType, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists)
        {
            return broker.CreateEndpoint(new EndpointCreationRequest
            {
                EndpointId = uniqueId,
                EndpointType = endpointType,
                ImplementationType = implType,
                Options = options
            });
        }
    }
}

using System.Runtime.Serialization;

namespace Spfx.Runtime.Exceptions
{
    [DataContract]
    public class EndpointNotFoundException : SerializableException
    {
        [DataMember]
        public string EndpointId { get; }

        public EndpointNotFoundException(ProcessEndpointAddress endpoint)
            : this(endpoint.ToString())
        {
        }

        public EndpointNotFoundException(string endpoint)
            : base("Endpoint not found: " + endpoint)
        {
            EndpointId = endpoint;
        }
    }
}
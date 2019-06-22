
using System.Runtime.Serialization;

namespace Spfx.Runtime.Exceptions
{
    [DataContract]
    public class EndpointAlreadyExistsException : SerializableException
    {
        [DataMember]
        public string EndpointId { get; }

        public EndpointAlreadyExistsException(string endpoint)
            : base("Endpoint already exists: " + endpoint)
        {
            EndpointId = endpoint;
        }
    }
}
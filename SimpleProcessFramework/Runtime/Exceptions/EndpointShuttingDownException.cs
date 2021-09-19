using System.Runtime.Serialization;

namespace Spfx.Runtime.Exceptions
{
    [DataContract]
    public class EndpointShuttingDownException : SerializableException
    {
        public EndpointShuttingDownException(string msg = null)
            : base(msg)
        {
        }
    }
}

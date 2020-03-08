using System.Runtime.Serialization;

namespace Spfx
{
    [DataContract]
    public class EndpointLostEventArgs
    {
        [DataMember]
        public ProcessEndpointAddress Address { get; }

        public EndpointLostEventArgs(ProcessEndpointAddress addr)
        {
            Address = addr;
        }
    }
}
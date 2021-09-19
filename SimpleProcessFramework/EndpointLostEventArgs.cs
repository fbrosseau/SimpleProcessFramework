using System.Runtime.Serialization;

namespace Spfx
{
    public enum EndpointLostReason
    {
        RemoteEndpointLost,
        LocalFailure,
        Destroyed
    }

    [DataContract]
    public class EndpointLostEventArgs
    {
        [DataMember]
        public ProcessEndpointAddress Address { get; }

        [DataMember]
        public ProcessEndpointAddress OriginalAddress { get; }

        [DataMember]
        public EndpointLostReason Reason { get; }

        public EndpointLostEventArgs(ProcessEndpointAddress addr, EndpointLostReason reason)
            : this(addr, addr, reason)
        {
        }

        public EndpointLostEventArgs(ProcessEndpointAddress originalAddress, ProcessEndpointAddress specificAddress, EndpointLostReason reason)
        {
            Address = specificAddress;
            OriginalAddress = originalAddress;
            Reason = reason;
        }
    }
}
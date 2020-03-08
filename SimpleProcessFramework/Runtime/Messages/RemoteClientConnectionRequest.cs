using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public sealed class RemoteClientConnectionRequest
    {
        public static int MaximumMessageSize = 32;

        [DataMember]
        public byte[] Salt { get; }

        [DataMember]
        public byte[] Secret { get; }
    }

}

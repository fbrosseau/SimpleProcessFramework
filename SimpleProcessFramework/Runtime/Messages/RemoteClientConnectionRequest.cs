using System.Runtime.Serialization;

namespace SimpleProcessFramework.Runtime.Messages
{
    [DataContract]
    public class RemoteClientConnectionRequest
    {
        public static int MaximumMessageSize = 32;

        [DataMember]
        public byte[] Salt { get; }

        [DataMember]
        public byte[] Secret { get; }
    }

}

using System.Runtime.Serialization;

namespace SimpleProcessFramework.Runtime.Messages
{
    [DataContract]
    public class RemoteClientConnectionResponse
    {
        [DataMember]
        public bool Success { get; set; }

        [DataMember]
        public string Error { get; set; }
    }

}

using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public sealed class RemoteClientConnectionResponse
    {
        [DataMember]
        public bool Success { get; set; }

        [DataMember]
        public string Error { get; set; }
    }

}

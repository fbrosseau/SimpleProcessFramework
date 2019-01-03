using System.Runtime.Serialization;

namespace SimpleProcessFramework.Runtime.Messages
{
    [DataContract]
    public sealed class RemoteCallSuccessResponse : RemoteInvocationResponse
    {
        [DataMember]
        public object Result { get; set; }
    }
}

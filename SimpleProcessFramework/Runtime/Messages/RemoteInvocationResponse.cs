using System.Runtime.Serialization;

namespace SimpleProcessFramework.Runtime.Messages
{
    [DataContract]
    public abstract class RemoteInvocationResponse : IInterprocessRequest
    {
        [DataMember]
        public long CallId { get; set; }

        public bool ExpectResponse => false;
    }
}
using System;
using System.Runtime.Serialization;

namespace SimpleProcessFramework.Runtime.Messages
{
    [DataContract]
    public abstract class RemoteInvocationRequest : IInterprocessRequest
    {
        [DataMember]
        public long CallId { get; set; }
        [DataMember]
        public ProcessEndpointAddress Destination { get; set; }
        [DataMember]
        public TimeSpan AbsoluteTimeout { get; set; }
    }
}

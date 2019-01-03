using System;
using System.Runtime.Serialization;

namespace SimpleProcessFramework.Runtime.Messages
{
    [DataContract]
    public sealed class RemoteCallFailureResponse : RemoteInvocationResponse
    {
        [DataMember]
        public Exception Error { get; set; }
    }
}

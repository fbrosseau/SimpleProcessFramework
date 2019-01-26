using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Messages
{
    [DataContract]
    public sealed class RemoteCallFailureResponse : RemoteInvocationResponse
    {
        [DataMember]
        public Exception Error { get; set; }

        internal override void ForwardResult(TaskCompletionSource<object> completion)
        {
            completion?.TrySetException(Error);
        }
    }
}

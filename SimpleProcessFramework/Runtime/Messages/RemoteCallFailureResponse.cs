using Spfx.Serialization;
using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public sealed class RemoteCallFailureResponse : RemoteInvocationResponse
    {
        [DataMember]
        public RemoteExceptionInfo Error { get; set; }

        internal override void ForwardResult(TaskCompletionSource<object> completion)
        {
            completion?.TrySetException(Error.RecreateException());
        }
    }
}

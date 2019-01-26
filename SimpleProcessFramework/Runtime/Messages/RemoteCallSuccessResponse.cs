using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Messages
{
    [DataContract]
    public sealed class RemoteCallSuccessResponse : RemoteInvocationResponse
    {
        [DataMember]
        public object Result { get; set; }

        internal override void ForwardResult(TaskCompletionSource<object> completion)
        {
            completion?.TrySetResult(Result);
        }
    }
}

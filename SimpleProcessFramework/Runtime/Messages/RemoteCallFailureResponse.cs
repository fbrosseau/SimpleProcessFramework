using Spfx.Serialization;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public sealed class RemoteCallFailureResponse : RemoteInvocationResponse
    {
        [DataMember]
        public IRemoteExceptionInfo Error { get; set; }

        internal override void ForwardResult(TaskCompletionSource<object> completion)
        {
            completion?.TrySetException(Error.RecreateException());
        }
    }
}

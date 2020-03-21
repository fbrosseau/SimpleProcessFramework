using Spfx.Serialization;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public sealed class RemoteCallFailureResponse : RemoteInvocationResponse
    {
        [DataMember]
        public IRemoteExceptionInfo Error { get; set; }

        internal override void ForwardResult(IInvocationResponseHandler completion)
        {
            completion?.TrySetException(Error.RecreateException());
        }

        public RemoteCallFailureResponse(long callId, IRemoteExceptionInfo ex)
            : base(callId)
        {
            Error = ex;
        }
    }
}

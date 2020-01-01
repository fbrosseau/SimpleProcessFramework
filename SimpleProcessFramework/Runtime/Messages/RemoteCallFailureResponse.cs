using Spfx.Serialization;
using Spfx.Utilities.Threading;
using System;
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

        private RemoteCallFailureResponse(long callId, Exception ex)
            : base(callId)
        {
            Error = RemoteExceptionInfo.Create(ex);
        }

        internal static RemoteInvocationResponse Create(long callId, Task completion)
        {
            if (completion.Status == TaskStatus.Canceled)
                return new RemoteCallCancelledResponse(callId);
            return Create(callId, completion.ExtractException());
        }

        internal static RemoteInvocationResponse Create(long callId, Exception exception)
        {
            return new RemoteCallFailureResponse(callId, exception);
        }
    }
}

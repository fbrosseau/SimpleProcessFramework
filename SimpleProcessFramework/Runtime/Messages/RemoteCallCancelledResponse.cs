using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public sealed class RemoteCallCancelledResponse : RemoteInvocationResponse
    {
        public RemoteCallCancelledResponse(long callId) 
            : base(callId)
        {
        }

        internal override void ForwardResult(IInvocationResponseHandler completion)
        {
            completion.TrySetCanceled();
        }
    }
}

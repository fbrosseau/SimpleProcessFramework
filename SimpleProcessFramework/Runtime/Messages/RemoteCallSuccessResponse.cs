using Spfx.Utilities;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public sealed class RemoteCallSuccessResponse : RemoteInvocationResponse
    {
        [DataMember]
        public object Result { get; set; }

        public override string GetTinySummaryString()
                 => nameof(RemoteCallSuccessResponse) + ":" + MostRandomUtilities.FormatObjectToTinyString(Result) + "(#" + CallId + ")";

        public RemoteCallSuccessResponse(long callId, object res)
            : base(callId)
        {
            Result = res;
        }

        internal override void ForwardResult(IInvocationResponseHandler completion)
        {
            completion?.TrySetResult(Result);
        }
    }
}
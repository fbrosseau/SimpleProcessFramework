using Spfx.Utilities;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public sealed class RemoteCallSuccessResponse : RemoteInvocationResponse
    {
        [DataMember]
        public object Result { get; set; }
        
        public override string GetTinySummaryString()
                 => nameof(RemoteCallSuccessResponse) + ":" + MostRandomUtilities.FormatObjectToTinyString(Result) + "(#" + CallId + ")";

        internal override void ForwardResult(TaskCompletionSource<object> completion)
        {
            completion?.TrySetResult(Result);
        }
    }
}
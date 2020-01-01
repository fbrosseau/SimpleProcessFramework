using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public abstract class RemoteInvocationResponse : IStatefulInterprocessMessage
    {
        [DataMember]
        public long CallId { get; set; }

        public ProcessEndpointAddress Destination { get; set; }

        protected RemoteInvocationResponse(long callId)
        {
            CallId = callId;
        }

        internal abstract void ForwardResult(TaskCompletionSource<object> completion);

        public override string ToString() => GetTinySummaryString();
        public virtual string GetTinySummaryString() => $"{GetType().Name}(#{CallId})";
    }
}
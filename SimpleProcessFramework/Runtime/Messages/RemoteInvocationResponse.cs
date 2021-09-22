using System;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    public interface IInvocationResponseHandler
    {
        bool TrySetResult(object result);
        bool TrySetException(Exception ex);
        bool TrySetCanceled();
    }

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

        internal abstract void ForwardResult(IInvocationResponseHandler completion);

        public override string ToString() => GetTinySummaryString();
        public virtual string GetTinySummaryString() => $"{GetType().Name}(#{CallId})";
    }
}
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public abstract class RemoteInvocationResponse : IInterprocessRequest
    {
        [DataMember]
        public long CallId { get; set; }

        public bool ExpectResponse => false;

        public ProcessEndpointAddress Destination { get; set; }

        internal abstract void ForwardResult(TaskCompletionSource<object> completion);
    }
}
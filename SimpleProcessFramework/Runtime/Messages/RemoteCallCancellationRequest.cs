using System.Runtime.Serialization;

namespace SimpleProcessFramework.Runtime.Messages
{
    [DataContract]
    public sealed class RemoteCallCancellationRequest : RemoteInvocationRequest
    {
    }
}

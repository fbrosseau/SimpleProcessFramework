using System.Runtime.Serialization;

namespace SimpleProcessFramework.Runtime.Messages
{
    [DataContract]
    public class EndpointDescriptionRequest : RemoteInvocationRequest
    {
        public override bool ExpectResponse => true;
    }
}

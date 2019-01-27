using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public class EndpointDescriptionRequest : RemoteInvocationRequest
    {
        public override bool ExpectResponse => true;
    }
}

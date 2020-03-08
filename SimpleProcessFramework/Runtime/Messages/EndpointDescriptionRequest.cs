using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public sealed class EndpointDescriptionRequest : RemoteInvocationRequest
    {
        public override bool ExpectResponse => true;
    }
}

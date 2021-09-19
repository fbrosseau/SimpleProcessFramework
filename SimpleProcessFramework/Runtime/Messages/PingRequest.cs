using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public sealed class PingRequest : RemoteInvocationRequest
    {
        public override bool ExpectResponse => true;
    }
}

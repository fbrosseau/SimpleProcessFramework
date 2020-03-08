using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public sealed class EventRegistrationRequest : RemoteInvocationRequest
    {
        public override bool ExpectResponse => true;

        [DataMember]
        public List<string> AddedEvents { get; set; }
        [DataMember]
        public List<string> RemovedEvents { get; set; }
    }
}

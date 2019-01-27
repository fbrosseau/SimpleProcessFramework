using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public class EventRegistrationRequest : RemoteInvocationRequest
    {
        public override bool ExpectResponse => true;

        [DataMember]
        public long RegistrationId { get; set; }
        [DataMember]
        public List<EventRegistrationItem> AddedEvents { get; set; }
        [DataMember]
        public List<string> RemovedEvents { get; set; }
    }

    [DataContract]
    public class EventRegistrationItem
    {
        [DataMember]
        public string EventName { get; set; }
        [DataMember]
        public long RegistrationId { get; set; }
    }
}

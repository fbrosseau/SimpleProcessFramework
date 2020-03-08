using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public sealed class EventRaisedMessage : IEventMessage
    {
        ProcessEndpointAddress IInterprocessMessage.Destination { get => null; set { } }

        [DataMember]
        public ProcessEndpointAddress EndpointId { get; set; }
        [DataMember]
        public long SubscriptionId { get; set; }
        [DataMember]
        public string EventName { get; set; }
        [DataMember]
        public object EventArgs { get; set; }

        public override string ToString() => GetTinySummaryString();
        public string GetTinySummaryString() => $"{nameof(EventRaisedMessage)} {EventName} (Sub {EndpointId})";
    }
}
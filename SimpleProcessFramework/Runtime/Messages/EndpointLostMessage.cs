using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public sealed class EndpointLostMessage : IEventMessage
    {
        ProcessEndpointAddress IInterprocessMessage.Destination { get => null; set { } }

        [DataMember]
        public ProcessEndpointAddress Endpoint { get; set; }

        public override string ToString() => GetTinySummaryString();
        public string GetTinySummaryString() => $"{nameof(EndpointLostMessage)} {Endpoint}";
    }
}
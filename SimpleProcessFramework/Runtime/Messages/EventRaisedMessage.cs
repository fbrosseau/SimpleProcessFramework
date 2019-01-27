﻿using System.Runtime.Serialization;

namespace SimpleProcessFramework.Runtime.Messages
{
    [DataContract]
    public class EventRaisedMessage : IInterprocessMessage
    {
        ProcessEndpointAddress IInterprocessMessage.Destination { get => null; set { } }

        [DataMember]
        public long SubscriptionId { get; set; }
        [DataMember]
        public string EventName { get; set; }
        [DataMember]
        public object EventArgs { get; set; }
    }
}
using System.Collections.Generic;

namespace Spfx.Runtime.Client.Eventing
{
    public class EventSubscriptionChangeRequest
    {
        public List<EventSubscriptionChange> Changes { get; } = new List<EventSubscriptionChange>();
    }
}
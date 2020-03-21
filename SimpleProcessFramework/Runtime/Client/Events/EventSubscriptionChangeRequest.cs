using System.Collections.Generic;

namespace Spfx.Runtime.Client.Events
{
    public class EventSubscriptionChangeRequest
    {
        public List<EventSubscriptionChange> Changes { get; } = new List<EventSubscriptionChange>();
    }
}
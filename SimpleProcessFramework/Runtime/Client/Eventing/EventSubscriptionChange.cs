namespace Spfx.Runtime.Client.Eventing
{
    public delegate void RawEventDelegate(object eventValue);

    public class EventSubscriptionChange
    {
        public ProcessEndpointAddress Endpoint { get; }
        public RawEventDelegate Handler { get; }
        public string Name { get; }
        public bool IsAdd { get; }

        public EventSubscriptionChange(ProcessEndpointAddress endpoint, RawEventDelegate handler, string name, bool isAdd)
        {
            Endpoint = endpoint;
            Handler = handler;
            Name = name;
            IsAdd = isAdd;
        }
    }
}

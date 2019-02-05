using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    public interface IInterprocessMessage
    {
        ProcessEndpointAddress Destination { get; set; }
    }

    public interface IStatefulInterprocessMessage : IInterprocessMessage
    {
        [DataMember]
        long CallId { get; set; }
    }

    public interface IInterprocessRequest : IStatefulInterprocessMessage
    {
        bool ExpectResponse { get; }
    }
}

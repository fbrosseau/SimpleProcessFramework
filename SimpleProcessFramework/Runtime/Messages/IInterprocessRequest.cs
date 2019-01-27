using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    public interface IInterprocessMessage
    {
        ProcessEndpointAddress Destination { get; set; }
    }

    public interface IInterprocessRequest : IInterprocessMessage
    {
        [DataMember]
        long CallId { get; set; }

        bool ExpectResponse { get; }
    }
}

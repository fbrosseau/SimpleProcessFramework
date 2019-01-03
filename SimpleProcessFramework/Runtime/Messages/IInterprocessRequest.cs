using System.Runtime.Serialization;

namespace SimpleProcessFramework.Runtime.Messages
{
    public interface IInterprocessRequest
    {
        [DataMember]
        long CallId { get; set; }

        bool ExpectResponse { get; }
    }
}

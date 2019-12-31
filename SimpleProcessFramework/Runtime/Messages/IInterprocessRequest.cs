using Spfx.Utilities;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    public interface IInterprocessMessage
    {
        ProcessEndpointAddress Destination { get; set; }

        string GetTinySummaryString();
    }

    public interface IStatefulInterprocessMessage : IInterprocessMessage
    {
        [DataMember]
        long CallId { get; set; }
    }

    public static class StatefulInterprocessMessageExtensions
    {
        public static long GetValidCallId(this IStatefulInterprocessMessage msg)
        {
            Guard.ArgumentNotNull(msg, nameof(msg));
            var id = msg.CallId;
            Debug.Assert(id != SimpleUniqueIdFactory.InvalidId, "Invalid call ID!");
            return id;
        }
    }

    public interface IInterprocessRequest : IStatefulInterprocessMessage
    {
        bool ExpectResponse { get; }
    }
}

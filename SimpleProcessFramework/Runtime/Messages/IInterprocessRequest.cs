﻿using Spfx.Serialization;
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

    internal static class StatefulInterprocessMessageExtensions
    {
        public static IInterprocessMessage Unwrap(this IInterprocessMessage msg, IBinarySerializer serializer)
        {
            return WrappedInterprocessMessage.Unwrap(msg, serializer);
        }

        public static bool HasValidCallId(this IStatefulInterprocessMessage msg)
        {
            return IsValidCallId(msg.CallId);
        }

        public static bool IsValidCallId(long id)
        {
            return id != SimpleUniqueIdFactory.InvalidId;
        }

        public static long GetValidCallId(this IStatefulInterprocessMessage msg)
        {
            Guard.ArgumentNotNull(msg, nameof(msg));
            var id = msg.CallId;
            Debug.Assert(IsValidCallId(id), "Invalid call ID!");
            return id;
        }
    }

    public interface IInterprocessRequest : IStatefulInterprocessMessage
    {
        bool ExpectResponse { get; }
    }
}

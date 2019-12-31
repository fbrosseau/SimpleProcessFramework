using Spfx.Reflection;
using Spfx.Serialization;
using Spfx.Utilities;
using System.IO;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public class WrappedInterprocessMessage : IInterprocessMessage
    {
        [DataMember]
        public ProcessEndpointAddress Destination { get; set; }

        [DataMember]
        public ReflectedTypeInfo PayloadType { get; set; }

        [DataMember]
        public byte[] Payload { get; set; }

        [DataMember]
        public long CallId { get; set; }

        [DataMember]
        public string SourceConnectionId { get; set; }

        [DataMember]
        public bool IsRequest { get; set; }

        /// <summary>
        /// Provides no functional value, it is there to track messages across IPC hops
        /// </summary>
        [DataMember]
        public string TracingId { get; set; }

        public WrappedInterprocessMessage()
        {
        }

        public static WrappedInterprocessMessage Wrap(IInterprocessMessage msg, IBinarySerializer serializer)
        {
            if (msg is WrappedInterprocessMessage alreadyWrapped)
                return alreadyWrapped;

            var dest = msg.Destination;
            msg.Destination = null;

            long callId = 0;

            if (msg is IStatefulInterprocessMessage request)
            {
                callId = request.CallId;
            }

            string tracingId = null;
#if DEBUG
            tracingId = msg.GetTinySummaryString();
#endif

            var payload = serializer.SerializeToBytes(msg, lengthPrefix: false);
            return new WrappedInterprocessMessage
            {
                Destination = dest,
                PayloadType = msg.GetType(),
                Payload = payload,
                CallId = callId,
                IsRequest = msg is IInterprocessRequest,
                TracingId = tracingId
            };
        }

        public IInterprocessMessage Unwrap(IBinarySerializer serializer)
        {
            var msg = serializer.Deserialize<IInterprocessMessage>(new MemoryStream(Payload));
            msg.Destination = Destination;

            if (CallId != SimpleUniqueIdFactory.InvalidId)
            {
                ((IStatefulInterprocessMessage)msg).CallId = CallId;
            }

            return msg;
        }

        public string GetTinySummaryString()
        {
            if (!string.IsNullOrWhiteSpace(TracingId))
                return "Wrapped " + TracingId;
            return $"Wrapped {PayloadType.GetShortName()}#{CallId} ({Payload.Length} bytes)";
        }

        public override string ToString() => GetTinySummaryString();
    }
}

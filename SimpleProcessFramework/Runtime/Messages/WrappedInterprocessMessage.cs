using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Serialization;
using SimpleProcessFramework.Utilities;
using System.IO;
using System.Runtime.Serialization;

namespace SimpleProcessFramework.Runtime.Messages
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
        public long SourceConnectionId { get; set; }

        public static WrappedInterprocessMessage Wrap(IInterprocessMessage msg, IBinarySerializer serializer)
        {
            if (msg is WrappedInterprocessMessage alreadyWrapped)
                return alreadyWrapped;

            var dest = msg.Destination;
            msg.Destination = null;

            long callId = 0;

            var request = msg as IInterprocessRequest;
            if (request != null)
            {
                callId = request.CallId;
            }

            var payload = serializer.SerializeToBytes(msg, lengthPrefix: false);
            return new WrappedInterprocessMessage
            {
                Destination = dest,
                PayloadType = ReflectedTypeInfo.Create(msg.GetType()),
                Payload = payload,
                CallId = callId
            };
        }

        public IInterprocessMessage Unwrap(IBinarySerializer serializer)
        {
            var msg = serializer.Deserialize<IInterprocessMessage>(new MemoryStream(Payload));
            msg.Destination = Destination;

            if (CallId != SimpleUniqueIdFactory.InvalidId)
            {
                ((IInterprocessRequest)msg).CallId = CallId;
            }

            return msg;
        }
    }
}

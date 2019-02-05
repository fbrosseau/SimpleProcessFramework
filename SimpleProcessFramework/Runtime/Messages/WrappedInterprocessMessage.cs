﻿using Spfx.Reflection;
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
                PayloadType = msg.GetType(),
                Payload = payload,
                CallId = callId,
                IsRequest = msg is IInterprocessRequest
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
    }
}

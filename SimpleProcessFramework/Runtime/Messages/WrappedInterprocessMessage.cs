using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Serialization;
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

        public static WrappedInterprocessMessage Wrap(IInterprocessMessage msg, IBinarySerializer serializer)
        {
            var dest = msg.Destination;
            msg.Destination = null;

            var payload = serializer.SerializeToBytes(msg, lengthPrefix: false);
            return new WrappedInterprocessMessage
            {
                Destination = dest,
                PayloadType = ReflectedTypeInfo.Create(msg.GetType()),
                Payload = payload
            };
        }

        public IInterprocessMessage Unwrap(IBinarySerializer serializer)
        {
            var msg = serializer.Deserialize<IInterprocessMessage>(new MemoryStream(Payload));
            msg.Destination = Destination;
            return msg;
        }
    }
}

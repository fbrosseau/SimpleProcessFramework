using System;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Exceptions
{
    [DataContract]
    public class SerializableException : Exception
    {
        [DataMember]
        public override string Message { get; }

        public SerializableException()
        {
            Message = base.Message;
        }

        public SerializableException(string message)
            : base(message)
        {
            Message = base.Message;
        }

        public SerializableException(string message, Exception innerEx)
            : base(message, innerEx)
        {
            Message = base.Message;
        }
    }
}
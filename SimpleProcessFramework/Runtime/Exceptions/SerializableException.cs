using System;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Exceptions
{
    [DataContract]
    public class SerializableException : Exception
    {
        [DataMember]
        private string m_message;

        public override string Message => m_message;

        public SerializableException()
        {
            m_message = base.Message;
        }

        public SerializableException(string message)
            : base(message)
        {
            m_message = base.Message;
        }

        public SerializableException(string message, Exception innerEx)
            : base(message, innerEx)
        {
            m_message = base.Message;
        }
    }
}
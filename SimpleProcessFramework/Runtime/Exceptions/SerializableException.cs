using Spfx.Serialization;
using Spfx.Serialization.DataContracts;
using System;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Exceptions
{
    [DataContract]
    public class RemoteException : SerializableException
    {
        protected override bool TrackRemoteStackTrace => true;

        public RemoteException()
        {
        }

        public RemoteException(string message)
            : base(message)
        {
        }

        public RemoteException(string message, Exception innerEx)
            : base(message, innerEx)
        {
        }
    }

    [DataContract]
    public class SerializableException : Exception, ISerializationAwareObject
    {
        private string m_remoteStackTrace;
        private bool m_wasDeserialized;

        [DataMember]
        public override string Message { get; }

        protected virtual bool TrackRemoteStackTrace => false;

        [DataMember]
        public string RemoteStackTrace
        {
            get
            {
                if (m_remoteStackTrace is null && (m_wasDeserialized || TrackRemoteStackTrace))
                    RemoteStackTrace = StackTrace ?? "";

                return m_remoteStackTrace;
            }
            private set /* used by serialization */
            {
                m_remoteStackTrace = value;
            }
        }

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

        void ISerializationAwareObject.OnBeforeSerialize(SerializerSession session)
        {
        }

        void ISerializationAwareObject.OnAfterSerialize(SerializerSession session)
        {
        }

        void ISerializationAwareObject.OnBeforeDeserialize(DeserializerSession session)
        {
            m_wasDeserialized = true;
        }

        void ISerializationAwareObject.OnAfterDeserialize(DeserializerSession session)
        {
        }
    }
}
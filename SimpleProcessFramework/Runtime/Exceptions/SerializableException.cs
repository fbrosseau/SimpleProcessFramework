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
    public class SerializableException : Exception
    {
        private string m_remoteStackTrace;

        [DataMember]
        public override string Message { get; }

        protected virtual bool TrackRemoteStackTrace => false;

        [DataMember]
        public string RemoteStackTrace
        {
            get
            {
                if (m_remoteStackTrace is null && TrackRemoteStackTrace)
                    m_remoteStackTrace = StackTrace ?? "";

                return m_remoteStackTrace;
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
    }
}
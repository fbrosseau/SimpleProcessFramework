using SimpleProcessFramework.Serialization;
using System;
using System.IO;

namespace SimpleProcessFramework.Runtime.Server
{
    internal class ServerRemoteInterprocessConnection : IInterprocessClientChannel
    {
        private readonly IBinarySerializer m_serializer;
        private readonly Stream m_stream;
        private readonly string m_localEndpoint;
        private readonly string m_remoteEndpoint;

        public ServerRemoteInterprocessConnection(IBinarySerializer serializer, Stream stream, string localEndpoint, string remoteEndpoint)
        {
            m_serializer = serializer;
            m_stream = stream;
            m_localEndpoint = localEndpoint;
            m_remoteEndpoint = remoteEndpoint;
        }

        public void SendFailure(long callId, Exception fault)
        {
            throw new NotImplementedException();
        }

        public void SendResponse(long callId, object completion)
        {
            throw new NotImplementedException();
        }
    }
}

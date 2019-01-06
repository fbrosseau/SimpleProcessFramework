using SimpleProcessFramework.Runtime.Common;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Serialization;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Server
{
    internal class ServerInterprocessChannel : AbstractInterprocessConection, IInterprocessClientChannel
    {
        private readonly Stream m_readStream;
        private readonly Stream m_writeStream;
        private readonly string m_localEndpoint;
        private readonly string m_remoteEndpoint;
        private readonly IInterprocessClientProxy m_wrapperProxy;
        private IClientRequestHandler m_owner;

        public Guid ConnectionId { get; } = Guid.NewGuid();

        public ServerInterprocessChannel(IBinarySerializer serializer, Stream duplexStream, string localEndpoint, string remoteEndpoint)
            : this(serializer, duplexStream, duplexStream, localEndpoint, remoteEndpoint)
        {
        }

        public ServerInterprocessChannel(IBinarySerializer serializer, Stream readStream, Stream writeStream, string localEndpoint, string remoteEndpoint)
            : base(serializer)
        {
            m_readStream = readStream;
            m_writeStream = writeStream;
            m_localEndpoint = localEndpoint;
            m_remoteEndpoint = remoteEndpoint;
            m_wrapperProxy = new SameProcessClientProxy(this);
        }

        internal override Task<(Stream readStream, Stream writeStream)> ConnectStreamsAsync()
        {
            return Task.FromResult((m_readStream, m_writeStream));
        }

        public virtual void Initialize(IClientRequestHandler clientConnectionsManager)
        {
            if (m_owner != null)
                throw new InvalidOperationException("This channel is already registered");
            m_owner = clientConnectionsManager;

            Initialize();
        }

        protected override void HandleMessage(IInterprocessMessage msg)
        {
            switch (msg)
            {
                case WrappedInterprocessMessage wrapper:
                    m_owner.OnRequestReceived(this, wrapper);
                    break;
                default:
                    base.HandleMessage(msg);
                    break;
            }
        }

        public void SendFailure(long callId, Exception fault)
        {
            SerializeAndSendMessage(new RemoteCallFailureResponse
            {
                CallId = callId,
                Error = fault
            });
        }

        public void SendResponse(long callId, object result)
        {
            SerializeAndSendMessage(new RemoteCallSuccessResponse
            {
                CallId = callId,
                Result = result
            });
        }

        private class SameProcessClientProxy : IInterprocessClientProxy
        {
            private IInterprocessClientChannel m_actualChannel;

            public SameProcessClientProxy(IInterprocessClientChannel actualChannel)
            {
                m_actualChannel = actualChannel;
            }

            public Task<IInterprocessClientChannel> GetClientInfo()
            {
                return Task.FromResult(m_actualChannel);
            }

            public void SendFailure(long callId, Exception fault)
            {
                m_actualChannel.SendFailure(callId, fault);
            }

            public void SendResponse(long callId, object completion)
            {
                m_actualChannel.SendResponse(callId, completion);
            }
        }

        public IInterprocessClientProxy GetWrapperProxy() => m_wrapperProxy;
    }
}
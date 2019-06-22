using Spfx.Runtime.Common;
using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server
{
    internal class ServerInterprocessChannel : AbstractInterprocessConection, IInterprocessClientChannel
    {
        public string UniqueId { get; }

        private readonly Stream m_readStream;
        private readonly Stream m_writeStream;
        private readonly string m_localEndpoint;
        private readonly string m_remoteEndpoint;
        private readonly IInterprocessClientProxy m_wrapperProxy;
        private IClientRequestHandler m_owner;
        private static readonly SimpleUniqueIdFactory<IInterprocessClientChannel> s_idFactory = new SimpleUniqueIdFactory<IInterprocessClientChannel>();

        public ServerInterprocessChannel(IBinarySerializer serializer, Stream duplexStream, string localEndpoint, string remoteEndpoint)
            : this(serializer, duplexStream, duplexStream, localEndpoint, remoteEndpoint)
        {
        }

        public ServerInterprocessChannel(IBinarySerializer serializer, Stream readStream, Stream writeStream, string localEndpoint, string remoteEndpoint)
            : base(serializer)
        {
            UniqueId = "<out>/" + s_idFactory.GetNextId(this);
            m_readStream = readStream;
            m_writeStream = writeStream;
            m_localEndpoint = localEndpoint;
            m_remoteEndpoint = remoteEndpoint;
            m_wrapperProxy = new ConcreteClientProxy(this);
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

        protected override void HandleExternalMessage(IInterprocessMessage msg)
        {
            switch (msg)
            {
                case WrappedInterprocessMessage wrapper:
                    m_owner.OnRequestReceived(this, wrapper);
                    break;
                default:
                    base.HandleExternalMessage(msg);
                    break;
            }
        }

        public void SendFailure(long callId, Exception fault)
        {
            SendMessage(new RemoteCallFailureResponse
            {
                CallId = callId,
                Error = RemoteExceptionInfo.Create(fault)
            });
        }

        public void SendResponse(long callId, object result)
        {
            SendMessage(new RemoteCallSuccessResponse
            {
                CallId = callId,
                Result = result
            });
        }

        public void SendMessage(IInterprocessMessage msg)
        {
            SerializeAndSendMessage(msg);
        }

        public IInterprocessClientProxy GetWrapperProxy() => m_wrapperProxy;
    }
}
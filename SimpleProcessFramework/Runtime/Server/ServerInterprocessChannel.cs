using Spfx.Reflection;
using Spfx.Runtime.Common;
using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server
{
    internal class ServerInterprocessChannel : AbstractInterprocessConnection, IInterprocessClientChannel
    {
        public string UniqueId { get; }

        private readonly Stream m_readStream;
        private readonly Stream m_writeStream;
        private readonly string m_localEndpoint;
        private readonly string m_remoteEndpoint;
        private readonly ITypeResolver m_typeResolver;
        private readonly IInterprocessClientProxy m_wrapperProxy;
        private IIncomingClientMessagesHandler m_messagesHandler;
        private static readonly SimpleUniqueIdFactory<IInterprocessClientChannel> s_idFactory = new SimpleUniqueIdFactory<IInterprocessClientChannel>();

        public ServerInterprocessChannel(ITypeResolver typeResolver, Stream duplexStream, string localEndpoint, string remoteEndpoint)
            : this(typeResolver, duplexStream, duplexStream, localEndpoint, remoteEndpoint)
        {
        }

        public ServerInterprocessChannel(ITypeResolver typeResolver, Stream readStream, Stream writeStream, string localEndpoint, string remoteEndpoint)
            : base(typeResolver)
        {
            UniqueId = InterprocessConnectionId.ExternalConnectionIdPrefix + "/ " + s_idFactory.GetNextId(this) + "/" + remoteEndpoint;
            m_readStream = readStream;
            m_writeStream = writeStream;
            m_localEndpoint = localEndpoint;
            m_remoteEndpoint = remoteEndpoint;
            m_typeResolver = typeResolver;
            m_wrapperProxy = new ConcreteClientProxy(this);
        }

        internal override Task<(Stream readStream, Stream writeStream)> ConnectStreamsAsync()
        {
            return Task.FromResult((m_readStream, m_writeStream));
        }

        void IInterprocessClientChannel.Initialize()
        {
            m_messagesHandler = m_typeResolver.GetSingleton<IIncomingClientMessagesHandler>();
            Initialize();
        }

        protected override void HandleExternalMessage(IInterprocessMessage msg)
        {
            switch (msg)
            {
                case WrappedInterprocessMessage wrapper:
                    m_messagesHandler.ForwardMessage(GetWrapperProxy(), wrapper);
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
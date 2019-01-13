using System;
using System.Threading.Tasks;
using SimpleProcessFramework.Runtime.Messages;

namespace SimpleProcessFramework.Runtime.Server
{
    public interface IInterprocessClientProxy
    {
        long UniqueId { get; }

        Task<IInterprocessClientChannel> GetClientInfo();

        void SendFailure(long callId, Exception fault);
        void SendResponse(long callId, object completion);
    }

    public interface IInterprocessClientChannel
    {
        long UniqueId { get; }

        event EventHandler ConnectionLost;
        void Initialize(IClientRequestHandler clientConnectionsManager);
        IInterprocessClientProxy GetWrapperProxy();

        void SendFailure(long callId, Exception fault);
        void SendResponse(long callId, object completion);
        void SendMessage(IInterprocessMessage msg);
    }
}
using System;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Server
{
    public interface IInterprocessClientProxy
    {
        Task<IInterprocessClientChannel> GetClientInfo();

        void SendFailure(long callId, Exception fault);
        void SendResponse(long callId, object completion);
    }

    public interface IInterprocessClientChannel
    {
        Guid ConnectionId { get; }
        event EventHandler ConnectionLost;
        void Initialize(IClientRequestHandler clientConnectionsManager);
        IInterprocessClientProxy GetWrapperProxy();

        void SendFailure(long callId, Exception fault);
        void SendResponse(long callId, object completion);
    }
}
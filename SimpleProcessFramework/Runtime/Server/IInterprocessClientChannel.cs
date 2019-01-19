using System;
using System.Threading.Tasks;
using SimpleProcessFramework.Runtime.Messages;

namespace SimpleProcessFramework.Runtime.Server
{
    public interface IInterprocessClientProxy
    {
        long UniqueId { get; }

        Task<IInterprocessClientChannel> GetClientInfo();

        void SendMessage(IInterprocessMessage msg);
    }

    public interface IInterprocessClientChannel
    {
        long UniqueId { get; }

        event EventHandler ConnectionLost;
        void Initialize(IClientRequestHandler clientConnectionsManager);
        IInterprocessClientProxy GetWrapperProxy();

        void SendMessage(IInterprocessMessage msg);
    }
}
using System;
using System.Threading.Tasks;
using Spfx.Runtime.Messages;

namespace Spfx.Runtime.Server
{
    public interface IInterprocessClientProxy
    {
        string UniqueId { get; }

        Task<IInterprocessClientChannel> GetClientInfo();

        void SendMessage(IInterprocessMessage msg);
    }

    public interface IInterprocessClientChannel
    {
        string UniqueId { get; }

        event EventHandler ConnectionLost;
        void Initialize(IClientRequestHandler clientConnectionsManager);
        IInterprocessClientProxy GetWrapperProxy();

        void SendMessage(IInterprocessMessage msg);
    }
}
using System;
using System.Threading.Tasks;
using Spfx.Runtime.Messages;

namespace Spfx.Runtime.Server
{
    public struct InterprocessConnectionId
    {
        public const string ExternalConnectionIdPrefix = "<out>/";

        public string UniqueId { get; }

        void SendMessage(IInterprocessMessage message)
        {

        }
    }

    public interface IInterprocessClientProxy
    {
        string UniqueId { get; }
        bool IsExternalConnection { get; }

        Task<IInterprocessClientChannel> GetClientInfo();

        void SendMessage(IInterprocessMessage msg);
    }

    public interface IInterprocessClientChannel
    {
        string UniqueId { get; }

        event EventHandler ConnectionLost;
        void Initialize();
        IInterprocessClientProxy GetWrapperProxy();

        void SendMessageToClient(IInterprocessMessage msg);
    }
}
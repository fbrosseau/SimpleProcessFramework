using Spfx.Runtime.Messages;
using Spfx.Utilities.Threading;
using System;
using System.Threading.Tasks;

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

        ValueTask<IInterprocessClientChannel> GetClientInfo();

        void SendMessage(IInterprocessMessage msg);
    }

    public interface IInterprocessClientChannel : IAsyncDestroyable
    {
        string UniqueId { get; }

        event EventHandler ConnectionLost;
        void Initialize();
        IInterprocessClientProxy GetWrapperProxy();

        void SendMessageToClient(IInterprocessMessage msg);
    }
}
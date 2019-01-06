using SimpleProcessFramework.Runtime.Messages;

namespace SimpleProcessFramework.Runtime.Server
{
    public interface IClientRequestHandler
    {
        void OnRequestReceived(IInterprocessClientChannel serverInterprocessChannel, WrappedInterprocessMessage wrapper);
    }
}
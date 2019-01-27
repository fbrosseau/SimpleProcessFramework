using Spfx.Runtime.Messages;

namespace Spfx.Runtime.Server
{
    public interface IClientRequestHandler
    {
        void OnRequestReceived(IInterprocessClientChannel serverInterprocessChannel, WrappedInterprocessMessage wrapper);
    }
}
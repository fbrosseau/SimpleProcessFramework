using Spfx.Runtime.Messages;

namespace Spfx.Runtime.Server
{
    public interface IIncomingClientMessagesHandler
    {
        void ForwardMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage);
    }
}
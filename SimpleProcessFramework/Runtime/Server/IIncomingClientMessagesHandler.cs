using Spfx.Runtime.Messages;
using System;

namespace Spfx.Runtime.Server
{
    public interface IIncomingClientMessagesHandler
    {
        void ForwardMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage);
        
        void AddProcessLostHandler(string processId, Action<EndpointLostEventArgs> onProcessLost);
        void RemoveProcessLostHandler(string subscribedProcess, Action<EndpointLostEventArgs> onProcessLost);
    }
}
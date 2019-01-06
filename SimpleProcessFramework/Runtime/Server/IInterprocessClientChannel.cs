using System;

namespace SimpleProcessFramework.Runtime.Server
{
    public interface IInterprocessClientChannel
    {
        void SendFailure(long callId, Exception fault);
        void SendResponse(long callId, object completion);
    }
}
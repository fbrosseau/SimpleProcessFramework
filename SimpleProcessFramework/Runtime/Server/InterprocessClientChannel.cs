using SimpleProcessFramework.Runtime.Messages;
using System;

namespace SimpleProcessFramework.Runtime.Server
{
    internal class InterprocessClientChannel : IInterprocessClientChannel
    {
        public void SendFailure(long callId, Exception fault)
        {
            Send(new RemoteCallFailureResponse
            {
                CallId = callId,
                Error = fault
            });
        }

        public void SendResponse(long callId, object result)
        {
            Send(new RemoteCallSuccessResponse
            {
                CallId = callId,
                Result = result
            });
        }

        private void Send(RemoteInvocationResponse msg)
        {
            throw new NotImplementedException();
        }
    }
}
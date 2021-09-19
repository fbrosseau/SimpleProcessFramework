using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities.Threading;
using System;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server
{
    public interface IFailureCallResponsesFactory
    {
        RemoteInvocationResponse Create(long callId, Task completion);
        RemoteInvocationResponse Create(long callId, Exception ex);
    }

    public class DefaultFailureCallResponsesFactory : IFailureCallResponsesFactory
    {
        public virtual bool ExposeRemoteCallstacks => true;

        public RemoteInvocationResponse Create(long callId, Task completion)
        {
            if (completion.Status == TaskStatus.Canceled)
                return new RemoteCallCancelledResponse(callId);
            return Create(callId, completion.ExtractException());
        }

        public RemoteInvocationResponse Create(long callId, Exception ex)
        {
            return new RemoteCallFailureResponse(callId, RemoteExceptionInfo.Create(ex, ExposeRemoteCallstacks));
        }
    }
}

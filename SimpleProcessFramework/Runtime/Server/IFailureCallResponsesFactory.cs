using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server
{
    public interface IFailureCallResponsesFactory
    {
        RemoteInvocationResponse Create(long callId, Task completion);
    }

    public class DefaultFailureCallResponsesFactory : IFailureCallResponsesFactory
    {
        public virtual bool ExposeRemoteCallstacks => true;

        public RemoteInvocationResponse Create(long callId, Task completion)
        {
            if (completion.Status == TaskStatus.Canceled)
                return new RemoteCallCancelledResponse(callId);
            return new RemoteCallFailureResponse(callId, RemoteExceptionInfo.Create(completion.ExtractException(), ExposeRemoteCallstacks));
        }
    }
}

using SimpleProcessFramework.Runtime.Messages;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Server
{
    public interface IInterprocessRequestContext : IDisposable
    {
        IInterprocessRequest Request { get; }
        IInterprocessClientContext Client { get; }

        CancellationToken Cancellation { get; }

        Task<object> Completion { get; }

        void SetupCancellationToken();

        void CompleteWithTask(Task t);
        void CompleteWithTaskOfT<T>(Task<T> t);

        void Respond(object o);
        void Fail(Exception ex);
        void Cancel();
    }
}
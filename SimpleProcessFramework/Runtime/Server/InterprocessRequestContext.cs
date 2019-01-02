using Oopi.Utilities;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Utilities;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Server
{
    internal class InterprocessRequestContext : IInterprocessRequestContext
    {
        private readonly ProcessEndpointHandler m_handler;
        private CancellationTokenSource m_cts;
        private readonly TaskCompletionSource<object> m_tcs;

        public IInterprocessRequest Request { get; }
        public IInterprocessClientContext Client { get; }
        public CancellationToken Cancellation => m_cts?.Token ?? default;
        public Task<object> Completion => m_tcs.Task;

        public InterprocessRequestContext(ProcessEndpointHandler endpointHandler, IInterprocessClientContext client, IInterprocessRequest req)
        {
            Guard.ArgumentNotNull(endpointHandler, nameof(endpointHandler));
            Guard.ArgumentNotNull(client, nameof(client));
            Guard.ArgumentNotNull(req, nameof(req));

            Client = client;
            Request = req;
            m_handler = endpointHandler;
            m_tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            m_tcs.Task.ContinueWith((t, s) => ((InterprocessRequestContext)s).MarkAsCompleted(), this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        public void Cancel()
        {
            m_cts?.SafeCancelAsync();
        }

        public void Dispose()
        {
            Cancel();
            m_cts?.Dispose();
        }

        public void Fail(Exception ex)
        {
            m_tcs.TrySetException(ex);
        }

        public void SetupCancellationToken()
        {
            var remoteCall = (RemoteInvocationRequest)Request;
            if (!remoteCall.Cancellable)
                return;

            m_cts = new CancellationTokenSource();

            if (remoteCall.HasTimeout)
                m_cts.CancelAfter(remoteCall.AbsoluteTimeout);
        }

        public void Respond(object o)
        {
            m_tcs.TrySetResult(o);
        }

        private void MarkAsCompleted()
        {
            m_handler.CompleteCall(this);
            Client.CallbackChannel.SendResponse(Request.CallId, Completion);
        }

        public void CompleteWithTask(Task t)
        {
            CompleteWithTask<VoidType>(t);
        }

        public void CompleteWithTaskOfT<T>(Task<T> t)
        {
            CompleteWithTask<T>(t);
        }

        private void CompleteWithTask<T>(Task t)
        {
            m_tcs.CompleteWithResultAsObject<T>(t);
        }

        internal static class Reflection
        {
            public static MethodInfo CompleteWithTaskMethod => typeof(IInterprocessRequestContext)
                .FindUniqueMethod(nameof(CompleteWithTask));
            public static MethodInfo GetCompleteWithTaskOfTMethod(Type resultType) => typeof(IInterprocessRequestContext)
                .FindUniqueMethod(nameof(CompleteWithTaskOfT)).MakeGenericMethod(resultType);
            public static MethodInfo Get_RequestMethod => typeof(IInterprocessRequestContext)
                .GetProperty(nameof(Request)).GetGetMethod();
            public static MethodInfo Get_CancellationMethod => typeof(IInterprocessRequestContext)
                .GetProperty(nameof(Cancellation)).GetGetMethod();
        }
    }
}
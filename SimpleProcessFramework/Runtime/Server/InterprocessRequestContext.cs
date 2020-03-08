using Spfx.Utilities;
using Spfx.Reflection;
using Spfx.Runtime.Messages;
using Spfx.Utilities.Threading;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Diagnostics;

namespace Spfx.Runtime.Server
{
    internal class InterprocessRequestContext : IInterprocessRequestContext
    {
        private static readonly Action<Task<object>, object> s_rawOnRequestCompleted = RawOnRequestCompleted;
        private readonly IProcessEndpointHandler m_handler;
        private CancellationTokenSource m_cts;
        private readonly TaskCompletionSource<object> m_tcs;
        private readonly ITypeResolver m_typeResolver;

        public IInterprocessRequest Request { get; }
        public IInterprocessClientProxy Client { get; }
        public CancellationToken Cancellation => m_cts?.Token ?? default;
        public Task<object> Completion => m_tcs.Task;

        public InterprocessRequestContext(ITypeResolver typeResolver, IProcessEndpointHandler endpointHandler, IInterprocessClientProxy client, IInterprocessRequest req)
        {
            Guard.ArgumentNotNull(endpointHandler, nameof(endpointHandler));
            Guard.ArgumentNotNull(client, nameof(client));
            Guard.ArgumentNotNull(req, nameof(req));

            Client = client;
            Request = req;
            m_handler = endpointHandler;
            m_tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            m_typeResolver = typeResolver;

            m_tcs.Task.ContinueWith(s_rawOnRequestCompleted, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private static void RawOnRequestCompleted(Task<object> t, object s)
        {
            ((InterprocessRequestContext)s).SafeMarkAsCompleted();
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
            
            if (Completion.IsCompletedSuccessfully())
            {
                Client.SendMessage(new RemoteCallSuccessResponse(Request.CallId, Completion.Result));
            }
            else
            {
                Client.SendMessage(RemoteCallFailureResponse.Create(Request.CallId, Completion));
            }
        }

        private void SafeMarkAsCompleted()
        {
            CriticalTryCatch.Run(m_typeResolver, this, thisInstance => thisInstance.MarkAsCompleted());
        }

        public void CompleteWithTask(Task t) => CompleteWithTask<VoidType>(t);
        public void CompleteWithValueTask(ValueTask t) => CompleteWithTask<VoidType>(t.AsTask());
        public void CompleteWithTaskOfT<T>(Task<T> t) => CompleteWithTask<T>(t);
        public void CompleteWithValueTaskOfT<T>(ValueTask<T> t) => CompleteWithTask<T>(t.AsTask());

        private void CompleteWithTask<T>(Task t)
        {
            if (t is null)
                throw new InvalidOperationException("Methods cannot return null Tasks");

            m_tcs.CompleteWith<T>(t);
        }

        public override string ToString()
        {
            return Request.GetTinySummaryString() + " -> " + Client.UniqueId;
        }

        internal static class Reflection
        {
            public static MethodInfo CompleteWithTaskMethod => typeof(IInterprocessRequestContext)
                .FindUniqueMethod(nameof(IInterprocessRequestContext.CompleteWithTask));
            public static MethodInfo CompleteWithValueTaskMethod => typeof(IInterprocessRequestContext)
                .FindUniqueMethod(nameof(IInterprocessRequestContext.CompleteWithValueTask));
            public static MethodInfo GetCompleteWithTaskOfTMethod(Type resultType) => typeof(IInterprocessRequestContext)
                .FindUniqueMethod(nameof(IInterprocessRequestContext.CompleteWithTaskOfT)).MakeGenericMethod(resultType);
            public static MethodInfo GetCompleteWithValueTaskOfTMethod(Type resultType) => typeof(IInterprocessRequestContext)
                .FindUniqueMethod(nameof(IInterprocessRequestContext.CompleteWithValueTaskOfT)).MakeGenericMethod(resultType);
            public static MethodInfo Get_RequestMethod => typeof(IInterprocessRequestContext)
                .GetProperty(nameof(IInterprocessRequestContext.Request)).GetGetMethod();
            public static MethodInfo Get_CancellationMethod => typeof(IInterprocessRequestContext)
                .GetProperty(nameof(IInterprocessRequestContext.Cancellation)).GetGetMethod();
        }
    }
}
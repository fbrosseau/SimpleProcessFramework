using Spfx.Diagnostics.Logging;
using Spfx.Reflection;
using Spfx.Runtime.Messages;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Common
{
    internal abstract class AbstractInterprocessConnection : AsyncDestroyable, IInterprocessConnection
    {
        public event EventHandler ConnectionLost;

        private Task m_writeTask;
        private readonly AsyncQueue<IPendingOperation> m_pendingWrites;
        private Exception m_caughtException;
        private readonly SimpleUniqueIdFactory<IPendingOperation> m_pendingRequests = new SimpleUniqueIdFactory<IPendingOperation>();
        protected ILogger Logger { get; }
        protected ITypeResolver TypeResolver { get; }

        protected AbstractInterprocessConnection(ITypeResolver typeResolver, string friendlyName = null)
        {
            Logger = typeResolver.GetLogger(GetType(), uniqueInstance: true, friendlyName);
            TypeResolver = typeResolver;
            
            m_pendingWrites = new AsyncQueue<IPendingOperation>
            {
                DisposeIgnoredItems = true
            };
        }

        protected override void OnDispose()
        {
            Logger.Info?.Trace("AbstractInterprocessConnection::OnDispose");
            
            var ex = m_caughtException;
            m_pendingWrites.Dispose(ex);

            var reqs = m_pendingRequests.DisposeAndGetAllValues();
            Logger.Info?.Trace($"Aborting {reqs.Length} ongoing requests");
            foreach (var r in reqs)
            {
                r.Abort(ex);
            }

            ConnectionLost?.Invoke(this, EventArgs.Empty);

            m_pendingWrites.Dispose(ex);
            m_writeTask?.FireAndForget();
            base.OnDispose();
            Logger.Dispose();
        }

        public abstract void Initialize();

        protected void BeginWrites()
        {
            Logger.Info?.Trace("BeginWrites");
            m_writeTask = DoWrites();
        }

        private async Task DoWrites()
        {
            try
            {
                await m_pendingWrites.ForEachAsync(ExecuteWrite);
            }
            catch (Exception ex)
            {
                HandleFailure(ex);
            }
        }

        protected abstract ValueTask ExecuteWrite(IPendingOperation op);

        protected virtual void HandleFailure(Exception ex)
        {
            Logger.Debug?.Trace(ex, "HandleFailure");
            m_caughtException = ex;
            Dispose();
        }

        protected virtual void ProcessReceivedMessage(IInterprocessMessage msg)
        {
            switch (msg)
            {
                case RemoteInvocationResponse callResponse:
                    {
                        using var op = m_pendingRequests.RemoveById(callResponse.GetValidCallId());
                        callResponse.ForwardResult(op);
                    }
                    break;
                default:
                    BadCodeAssert.ThrowInvalidOperation("Message of type " + msg.GetType().FullName + " is not handled");
                    break;
            }
        }

        protected void DiscardPendingRequest(long callId)
        {
            m_pendingRequests.RemoveById(callId)?.Dispose();
        }

        public Task<object> SerializeAndSendMessage(IInterprocessMessage msg, CancellationToken ct = default)
        {
            return SerializeAndSendMessage<object>(msg, ct);
        }

        public Task<TResult> SerializeAndSendMessage<TResult>(IInterprocessMessage msg, CancellationToken ct = default)
        {
            var op = CreatePendingOperation<TResult>(msg, ct);
            EnqueueOperation(op);
            return (Task<TResult>)op.Task;
        }

        protected virtual IPendingOperation CreatePendingOperation<TResult>(IInterprocessMessage msg, CancellationToken ct)
        {
            return new PendingOperation<TResult>(this, msg, ct);
        }

        protected void EnqueueOperation(IPendingOperation op)
        {
            if (op.Message is IInterprocessRequest req && req.ExpectResponse)
            {
                req.CallId = m_pendingRequests.GetNextId(op);
            }

            Logger.Debug?.Trace("EnqueueOperation " + op.Message.GetTinySummaryString());
            m_pendingWrites.Enqueue(op);
        }

        protected interface IPendingOperation : IAbortableItem, IInvocationResponseHandler
        {
            IInterprocessMessage Message { get; }
            Task Task { get; }
            AbstractInterprocessConnection Owner { get; }
        }

        protected class PendingOperation<TResult> : TaskCompletionSource<TResult>, IPendingOperation
        {
            public IInterprocessMessage Message { get; }
            public AbstractInterprocessConnection Owner { get; }
            public CancellationToken CancellationToken { get; }
            public ProcessEndpointAddress MessageDestination { get; }
            private readonly CancellationTokenRegistration m_cancellationTokenRegistration;

            Task IPendingOperation.Task => Task;

            public PendingOperation(AbstractInterprocessConnection owner, IInterprocessMessage req, CancellationToken ct)
                : base(req, TaskCreationOptions.RunContinuationsAsynchronously)
            {
                CancellationToken = ct;
                Message = req;
                Owner = owner;
                MessageDestination = req.Destination;

                if (CancellationToken.CanBeCanceled && (Message is IInterprocessRequest))
                    m_cancellationTokenRegistration = CancellationToken.Register(s => ((PendingOperation<TResult>)s).OnCancellationRequested(), this, false);
            }

            public void Abort(Exception ex)
            {
                if (ex != null)
                    TrySetException(ex);
                else
                    TrySetCanceled();

                m_cancellationTokenRegistration.DisposeAsync().FireAndForget();
            }

            public virtual void Dispose()
            {
                Abort(null);
            }

            private void OnCancellationRequested()
            {
                var req = (IInterprocessRequest)Message;
                Owner.Logger.Debug?.Trace("Call cancelled by source: " + req.GetTinySummaryString());
                Owner.SerializeAndSendMessage(new RemoteCallCancellationRequest
                {
                    CallId = req.CallId,
                    Destination = MessageDestination
                }).FireAndForget();
            }

            public virtual string GetTinySummaryString()
            {
                return Message.GetTinySummaryString();
            }

            public override string ToString() => GetTinySummaryString();

            public bool TrySetResult(object result)
            {
                try
                {
                    return base.TrySetResult((TResult)result);
                }
                catch(Exception ex)
                {
                    return TrySetException(ex);
                }
            }
        }
    }
}
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
    internal abstract class AbstractInterprocessConnection : Disposable, IInterprocessConnection
    {
        public event EventHandler ConnectionLost;

        private Task m_writeTask;
        private readonly AsyncQueue<PendingOperation> m_pendingWrites;
        private bool m_raisedConnectionLost;
        private readonly SimpleUniqueIdFactory<PendingOperation> m_pendingRequests = new SimpleUniqueIdFactory<PendingOperation>();
        protected ILogger Logger { get; }
        protected ITypeResolver TypeResolver { get; }

        protected AbstractInterprocessConnection(ITypeResolver typeResolver)
        {
            Logger = typeResolver.GetLogger(GetType(), uniqueInstance: true);
            TypeResolver = typeResolver;
            
            m_pendingWrites = new AsyncQueue<PendingOperation>
            {
                DisposeIgnoredItems = true
            };
        }

        protected override void OnDispose()
        {
            Logger.Info?.Trace("AbstractInterprocessConnection::OnDispose");
            m_pendingWrites.Dispose();
            m_writeTask?.FireAndForget();
            base.OnDispose();
            Logger.Dispose();
        }

        public abstract void Initialize();

        protected void BeginWrites()
        {
            Logger.Info?.Trace("BeginWrites");
            m_writeTask = m_pendingWrites.ForEachAsync(ExecuteWrite);
        }

        protected abstract ValueTask ExecuteWrite(PendingOperation op);

        protected void HandleFailure(Exception ex)
        {
            Logger.Debug?.Trace(ex, "HandleFailure");

            m_pendingWrites.Dispose(ex);

            lock (this)
            {
                if (m_raisedConnectionLost)
                    return;
                m_raisedConnectionLost = true;
            }

            ConnectionLost?.Invoke(this, EventArgs.Empty);
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
                    throw new InvalidOperationException("Message of type " + msg.GetType().FullName + " is not handled");
            }
        }

        protected void DiscardPendingRequest(long callId)
        {
            m_pendingRequests.RemoveById(callId)?.Dispose();
        }
        
        public Task<object> SerializeAndSendMessage(IInterprocessMessage msg, CancellationToken ct = default)
        {
            return EnqueueOperation(CreatePendingOperation(msg, ct));
        }

        protected virtual PendingOperation CreatePendingOperation(IInterprocessMessage msg, CancellationToken ct)
        {
            return new PendingOperation(this, msg, ct);
        }

        protected Task<object> EnqueueOperation(PendingOperation op)
        {
            if (op.Message is IInterprocessRequest req && req.ExpectResponse)
            {
                req.CallId = m_pendingRequests.GetNextId(op);
            }

            Logger.Debug?.Trace("EnqueueOperation " + op.Message.GetTinySummaryString());
            m_pendingWrites.Enqueue(op);
            return op.ExecuteToCompletion();
        }

        protected class PendingOperation : TaskCompletionSource<object>, IAbortableItem
        {
            public IInterprocessMessage Message { get; }
            public AbstractInterprocessConnection Owner { get; }
            public CancellationToken CancellationToken { get; }
            private readonly ProcessEndpointAddress m_originalAddress;

            public PendingOperation(AbstractInterprocessConnection owner, IInterprocessMessage req, CancellationToken ct)
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
                CancellationToken = ct;
                Message = req;
                Owner = owner;
                m_originalAddress = req.Destination;
            }

            protected PendingOperation()
            {
            }

            public void Abort(Exception ex)
            {
                TrySetException(ex);
            }

            public void Dispose()
            {
                TrySetCanceled();
            }

            internal Task<object> ExecuteToCompletion()
            {
                if (!CancellationToken.CanBeCanceled || !(Message is IInterprocessRequest))
                    return Task;
                return AwaitOperationWithCancellation();
            }

            private async Task<object> AwaitOperationWithCancellation()
            {
                using var ctRegistration = CancellationToken.Register(s => ((PendingOperation)s).OnCancellationRequested(), this, false);
                return await Task.ConfigureAwait(false);
            }

            private void OnCancellationRequested()
            {
                var req = (IInterprocessRequest)Message;
                Owner.Logger.Debug?.Trace("Call cancelled by source: " + req.GetTinySummaryString());
                Owner.SerializeAndSendMessage(new RemoteCallCancellationRequest
                {
                    CallId = req.CallId,
                    Destination = m_originalAddress
                }).FireAndForget();
            }

            public virtual string GetTinySummaryString()
            {
                return Message.GetTinySummaryString();
            }

            public override string ToString() => GetTinySummaryString();
        }
    }
}
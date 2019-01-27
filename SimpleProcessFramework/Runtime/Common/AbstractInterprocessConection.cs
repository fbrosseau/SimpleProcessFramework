using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Common
{
    internal abstract class AbstractInterprocessConection : IInterprocessConnection
    {
        public event EventHandler ConnectionLost;

        private readonly AsyncQueue<PendingOperation> m_pendingWrites;
        private Task m_writeFlow;
        private Task m_readFlow;
        private Task m_keepAliveTask;
        private bool m_raisedConnectionLost;

        protected Stream ReadStream { get; private set; }
        protected Stream WriteStream { get; private set; }
        protected IBinarySerializer BinarySerializer { get; }
        protected virtual TimeSpan KeepAliveInterval { get; } = TimeSpan.FromSeconds(30);

        protected class PendingOperation : IAbortableItem
        {
            public Stream Data { get; private set; }
            public TaskCompletionSource<object> Completion { get; }
            public IInterprocessMessage Request { get; }
            public CancellationToken CancellationToken { get; }

            public PendingOperation(IInterprocessMessage req, CancellationToken ct = default)
            {
                CancellationToken = ct;
                Request = req;
                Completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public void SerializeRequest(IBinarySerializer serializer)
            {
                Data = serializer.Serialize<IInterprocessMessage>(WrappedInterprocessMessage.Wrap(Request, serializer), lengthPrefix: true);
            }

            public PendingOperation(ConnectionCodes code)
            {
                Data = new MemoryStream(BitConverter.GetBytes((int)code));
            }

            public void Abort(Exception ex)
            {
                Completion.TrySetException(ex);
            }

            public void Dispose()
            {
                Data.Dispose();
                Completion.TrySetCanceled();
            }
        }

        public AbstractInterprocessConection(IBinarySerializer serializer)
        {
            BinarySerializer = serializer;

            m_pendingWrites = new AsyncQueue<PendingOperation>
            {
                DisposeIgnoredItems = true
            };
        }

        private async ValueTask ExecuteWrite(PendingOperation op)
        {
            try
            {
                try
                {
                    await DoWrite(op);                    
                }
                catch (Exception ex)
                {
                    op.Abort(ex);
                    HandleFailure(ex);
                }
            }
            catch
            {
                op.Dispose();
                throw;
            }
        }

        protected virtual ValueTask DoWrite(PendingOperation op)
        {
            return new ValueTask(op.Data.CopyToAsync(WriteStream));
        }

        protected void HandleFailure(Exception ex)
        {
            m_pendingWrites.Dispose(ex);

            lock (this)
            {
                if (m_raisedConnectionLost)
                    return;
                m_raisedConnectionLost = true;
            }

            ConnectionLost?.Invoke(this, EventArgs.Empty);
        }

        public virtual void Dispose()
        {
            ReadStream?.Dispose();
            WriteStream?.Dispose();
            m_pendingWrites.Dispose();
        }

        public void Initialize()
        {
            m_readFlow = Task.Run(ConnectAsync);
        }

        private async Task ConnectAsync()
        {
            try
            {
                (ReadStream, WriteStream) = await ConnectStreamsAsync();

                RescheduleKeepAlive();

                m_writeFlow = m_pendingWrites.ForEachAsync(i => ExecuteWrite(i));

                await ReceiveLoop();
            }
            catch (Exception ex)
            {
                HandleFailure(ex);
            }
        }

        private async Task ReceiveLoop()
        {
            try
            {
                while (true)
                {
                    var stream = await ReadStream.ReadLengthPrefixedBlock();
                    var msg = BinarySerializer.Deserialize<IInterprocessMessage>(stream);
                    HandleExternalMessage(msg);
                }
            }
            catch(Exception ex)
            {
                HandleFailure(ex);
            }
        }

        protected virtual void HandleExternalMessage(IInterprocessMessage msg)
        {
            throw new InvalidOperationException("Message of type " + msg.GetType().FullName + " is not handled");
        }

        private void RescheduleKeepAlive()
        {
            m_keepAliveTask = CreateKeepAliveTask();
        }

        private Task CreateKeepAliveTask()
        {
            var delay = KeepAliveInterval;
            if (delay == Timeout.InfiniteTimeSpan)
                return null;

            return DoKeepAlive(delay);
        }

        private async Task DoKeepAlive(TimeSpan delay)
        {
            await Task.Delay(delay);
         //   await EnqueueOperation(new PendingOperation(ConnectionCodes.KeepAlive));
            RescheduleKeepAlive();
        }

        internal enum ConnectionCodes
        {
            KeepAlive = -1
        }

        internal abstract Task<(Stream readStream, Stream writeStream)> ConnectStreamsAsync();

        public virtual Task<object> SerializeAndSendMessage(IInterprocessMessage msg, CancellationToken ct = default)
        {
            return EnqueueOperation(new PendingOperation(msg, ct));
        }

        protected async Task<object> EnqueueOperation(PendingOperation op)
        {
            op.SerializeRequest(BinarySerializer);
            m_pendingWrites.Enqueue(op);

            var ctRegistration = new CancellationTokenRegistration();

            if (op.CancellationToken.CanBeCanceled && op.Request is RemoteInvocationRequest req)
            {
                op.CancellationToken.Register(() => SerializeAndSendMessage(new RemoteCallCancellationRequest
                {
                    CallId = req.CallId,
                    Destination = req.Destination
                }).FireAndForget(), false);
            }

            using (ctRegistration)
            {
                return await op.Completion.Task.ConfigureAwait(false);
            }
        }
    }
}
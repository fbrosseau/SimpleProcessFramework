using Spfx.Diagnostics.Logging;
using Spfx.Reflection;
using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Common
{
    internal abstract class AbstractInterprocessConnection : Disposable, IInterprocessConnection
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

        private readonly ILogger m_logger;

        protected virtual TimeSpan KeepAliveInterval { get; }

        protected class PendingOperation : TaskCompletionSource<object>, IAbortableItem
        {
            private readonly ConnectionCodes? m_code;

            public IInterprocessMessage Request { get; }
            public AbstractInterprocessConnection Owner { get; }
            public CancellationToken CancellationToken { get; }

            public PendingOperation(AbstractInterprocessConnection owner, IInterprocessMessage req, CancellationToken ct)
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
                CancellationToken = ct;
                Request = req;
                Owner = owner;
            }

            public PendingOperation(ConnectionCodes code)
            {
                m_code = code;
            }

            public void Abort(Exception ex)
            {
                TrySetException(ex);
            }

            public void Dispose()
            {
                TrySetCanceled();
            }

            internal Stream CreateMessageStream(IBinarySerializer serializer)
            {
                if (Request != null)
                    return serializer.Serialize<IInterprocessMessage>(WrappedInterprocessMessage.Wrap(Request, serializer), lengthPrefix: true);
                return new MemoryStream(m_serializedCodes[m_code.Value], false);
            }

            private readonly Dictionary<ConnectionCodes, byte[]> m_serializedCodes =
                Enum.GetValues(typeof(ConnectionCodes))
                .Cast<ConnectionCodes>()
                .ToDictionary(c => c, c =>
                {
                    var arr = new byte[4];
                    BinaryPrimitives.WriteInt32LittleEndian(arr, (int)c);
                    return arr;
                });

            internal Task<object> ExecuteToCompletion()
            {
                if (!CancellationToken.CanBeCanceled || !(Request is IInterprocessRequest req))
                    return Task;

                return AwaitOperationWithCancellation(req);
            }

            private async Task<object> AwaitOperationWithCancellation(IInterprocessRequest req)
            {
                using var ctRegistration = CancellationToken.Register(() =>
                {
                    Owner.m_logger.Debug?.Trace("Call cancelled by source: " + req.GetTinySummaryString());
                    Owner.SerializeAndSendMessage(new RemoteCallCancellationRequest
                    {
                        CallId = req.CallId,
                        Destination = req.Destination
                    }).FireAndForget();
                }, false);

                return await Task.ConfigureAwait(false);
            }
        }

        public AbstractInterprocessConnection(ITypeResolver typeResolver)
        {
            BinarySerializer = typeResolver.CreateSingleton<IBinarySerializer>();

            m_logger = typeResolver.GetLogger(GetType(), uniqueInstance: true);

            var config = typeResolver.CreateSingleton<ProcessClusterConfiguration>();
            KeepAliveInterval = config.IpcConnectionKeepAliveInterval;

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
                    using var serializedData = op.CreateMessageStream(BinarySerializer);
                    m_logger.Debug?.Trace($"ExecuteWrite: {op.Request.GetTinySummaryString()} ({serializedData.Length} bytes)");
                    await DoWrite(op, serializedData).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    m_logger.Debug?.Trace(ex, "ExecuteWrite failed");
                    op.Abort(ex);
                    HandleFailure(ex);
                }
            }
            catch (Exception ex)
            {
                m_logger.Debug?.Trace(ex, "ExecuteWrite->HandleFailure failed");
                op.Dispose();
                throw;
            }
        }

        protected virtual ValueTask DoWrite(PendingOperation op, Stream dataStream)
        {
            return new ValueTask(dataStream.CopyToAsync(WriteStream));
        }

        protected void HandleFailure(Exception ex)
        {
            m_logger.Debug?.Trace(ex, "HandleFailure");

            m_pendingWrites.Dispose(ex);

            lock (this)
            {
                if (m_raisedConnectionLost)
                    return;
                m_raisedConnectionLost = true;
            }

            ConnectionLost?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnDispose()
        {
            m_logger.Info?.Trace("OnDispose");

            ReadStream?.Dispose();
            WriteStream?.Dispose();
            m_pendingWrites.Dispose();
            m_writeFlow?.FireAndForget();
            m_readFlow?.FireAndForget();
            m_keepAliveTask?.FireAndForget();
            base.OnDispose();
            m_logger.Dispose();
        }

        public void Initialize()
        {
            m_readFlow = Task.Run(ConnectAsync);
        }

        private async Task ConnectAsync()
        {
            try
            {
                m_logger.Info?.Trace("Begin ConnectStreams");
                (ReadStream, WriteStream) = await ConnectStreamsAsync();

                RescheduleKeepAlive();

                m_logger.Info?.Trace("ConnectStreams succeeded, starting Recv&Write loops");
                m_writeFlow = m_pendingWrites.ForEachAsync(ExecuteWrite);
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
                    using var stream = await ReadStream.ReadLengthPrefixedBlockAsync().ConfigureAwait(false);
                    var len = stream.Length;
                    var msg = BinarySerializer.Deserialize<IInterprocessMessage>(stream);
                    m_logger.Debug?.Trace($"Recv {msg.GetTinySummaryString()} ({len} bytes)");
                    HandleExternalMessage(msg);
                }
            }
            catch (Exception ex)
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
            m_logger.Debug?.Trace("RescheduleKeepAlive");
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
            await Task.Delay(delay).ConfigureAwait(false);
            m_logger.Debug?.Trace("DoKeepAlive");
            //   await EnqueueOperation(new PendingOperation(ConnectionCodes.KeepAlive));
            RescheduleKeepAlive();
        }

        internal enum ConnectionCodes
        {
            KeepAlive = -1
        }

        internal abstract Task<(Stream readStream, Stream writeStream)> ConnectStreamsAsync();

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
            m_logger.Debug?.Trace("EnqueueOperation " + op.Request.GetTinySummaryString());
            m_pendingWrites.Enqueue(op);
            return op.ExecuteToCompletion();
        }
    }
}
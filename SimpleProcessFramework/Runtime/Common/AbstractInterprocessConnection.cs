﻿using Spfx.Reflection;
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

        protected class PendingOperation : TaskCompletionSource<object>, IAbortableItem
        {
            public Stream Data { get; private set; }
            public IInterprocessMessage Request { get; }
            public CancellationToken CancellationToken { get; }

            public PendingOperation(IInterprocessMessage req, CancellationToken ct = default)
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
                CancellationToken = ct;
                Request = req;
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
                TrySetException(ex);
            }

            public void Dispose()
            {
                Data.Dispose();
                TrySetCanceled();
            }
        }

        public AbstractInterprocessConection(ITypeResolver typeResolver)
        {
            BinarySerializer = typeResolver.CreateSingleton<IBinarySerializer>();

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

            m_writeFlow?.FireAndForget();
            m_readFlow?.FireAndForget();
            m_keepAliveTask?.FireAndForget();
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
                    using var stream = await ReadStream.ReadLengthPrefixedBlockAsync();
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
                return await op.Task.ConfigureAwait(false);
            }
        }
    }
}
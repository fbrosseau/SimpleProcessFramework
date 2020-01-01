using Spfx.Io;
using Spfx.Reflection;
using Spfx.Runtime.Messages;
using Spfx.Serialization;
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
    internal abstract class StreamBasedInterprocessConnection : AbstractInterprocessConnection
    {
        private Task m_readTask;
        private Task m_keepAliveTask;

        protected Stream ReadStream { get; private set; }
        protected Stream WriteStream { get; private set; }
        protected IBinarySerializer BinarySerializer { get; }

        protected virtual TimeSpan KeepAliveInterval { get; }

        protected class PendingStreamOperation : PendingOperation
        {
            private readonly ConnectionCodes? m_code;

            public PendingStreamOperation(StreamBasedInterprocessConnection owner, IInterprocessMessage req, CancellationToken ct)
                : base(owner, req, ct)
            {
            }

            public PendingStreamOperation(ConnectionCodes code)
            {
                m_code = code;
            }

            internal virtual Stream CreateMessageStream(IBinarySerializer serializer)
            {
                if (Message != null)
                    return serializer.Serialize<IInterprocessMessage>(WrappedInterprocessMessage.Wrap(Message, serializer), lengthPrefix: true);
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

            public override string GetTinySummaryString()
            {
                if (Message != null)
                    return base.GetTinySummaryString();
                return $"<Code: {m_code.Value}>"; 
            }
        }

        public StreamBasedInterprocessConnection(ITypeResolver typeResolver)
            : base(typeResolver)
        {
            BinarySerializer = typeResolver.CreateSingleton<IBinarySerializer>();

            var config = typeResolver.CreateSingleton<ProcessClusterConfiguration>();
            KeepAliveInterval = config.IpcConnectionKeepAliveInterval;
        }

        protected override PendingOperation CreatePendingOperation(IInterprocessMessage msg, CancellationToken ct)
        {
            return new PendingStreamOperation(this, msg, ct);
        }

        protected override async ValueTask ExecuteWrite(PendingOperation op)
        {
            try
            {
                try
                {
                    using var serializedData = ((PendingStreamOperation)op).CreateMessageStream(BinarySerializer);
                    Logger.Debug?.Trace($"ExecuteWrite: {op.Message.GetTinySummaryString()} ({serializedData.Length} bytes)");
                    await DoWrite(op, serializedData).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Debug?.Trace(ex, "ExecuteWrite failed");
                    op.Abort(ex);
                    HandleFailure(ex);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug?.Trace(ex, "ExecuteWrite->HandleFailure failed");
                op.Dispose();
                throw;
            }
        }

        protected virtual ValueTask DoWrite(PendingOperation op, Stream dataStream)
        {
            return new ValueTask(dataStream.CopyToAsync(WriteStream));
        }

        protected override void OnDispose()
        {
            Logger.Info?.Trace($"{nameof(StreamBasedInterprocessConnection)}::OnDispose");

            ReadStream?.Dispose();
            WriteStream?.Dispose();
            m_readTask?.FireAndForget();
            m_keepAliveTask?.FireAndForget();
            base.OnDispose();
        }

        public override void Initialize()
        {
            m_readTask = Task.Run(ConnectAsync);
        }

        private async Task ConnectAsync()
        {
            try
            {
                Logger.Info?.Trace("Begin ConnectStreams");
                (ReadStream, WriteStream) = await ConnectStreamsAsync();

                RescheduleKeepAlive();

                Logger.Info?.Trace("ConnectStreams succeeded, starting Recv&Write loops");
                BeginWrites();
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
                    using var frame = await ReadStream.ReadCodeOrLengthPrefixedBlockAsync().ConfigureAwait(false);
                    if (frame.Code != null)
                    {
                        continue;
                    }

                    using var stream = frame.AcquireData();
                    var len = stream.Length;
                    var msg = BinarySerializer.Deserialize<IInterprocessMessage>(stream);
                    Logger.Debug?.Trace($"Recv {msg.GetTinySummaryString()} ({len} bytes)");
                    ProcessReceivedMessage(msg);
                }
            }
            catch (Exception ex)
            {
                HandleFailure(ex);
            }
        }

        private void RescheduleKeepAlive()
        {
            Logger.Debug?.Trace("RescheduleKeepAlive");
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
            Logger.Debug?.Trace("DoKeepAlive");
            //   await EnqueueOperation(new PendingOperation(ConnectionCodes.KeepAlive));
            RescheduleKeepAlive();
        }

        internal enum ConnectionCodes
        {
            KeepAlive = -1
        }

        internal abstract Task<(Stream readStream, Stream writeStream)> ConnectStreamsAsync();       
    }
}
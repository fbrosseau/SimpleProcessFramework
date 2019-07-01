using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Diagnostics.Logging;
using Spfx.Io;
using Spfx.Reflection;
using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server.Processes.Ipc
{
    internal abstract class IpcConnector : AsyncDestroyable
    {
        protected enum InterprocessFrameType
        {
            Handshake1 = 0xAA,
            Handshake2,
            Handshake3,

            IncomingRequest,
            OutgoingRequest,
            IncomingResponse,
            OutgoingResponse,
            IpcMessage,

            Teardown1,
            Teardown2,
        }

        protected static readonly IBinarySerializer s_binarySerializer = new DefaultBinarySerializer();

        protected ILogger Logger { get; }
        protected IIpcConnectorListener Owner { get; }
        protected ILengthPrefixedStreamReader ReadPipe { get; }
        protected ILengthPrefixedStreamWriter WritePipe { get; }
        protected IBinarySerializer BinarySerializer { get; }

        protected AsyncManualResetEvent Shutdown1ReceivedEvent { get; } = new AsyncManualResetEvent();
        protected AsyncManualResetEvent Shutdown2ReceivedEvent { get; } = new AsyncManualResetEvent();

        protected IpcConnector(IIpcConnectorListener owner, ILengthPrefixedStreamReader readPipe, ILengthPrefixedStreamWriter writePipe, ITypeResolver typeResolver)
        {
            Guard.ArgumentNotNull(owner, nameof(owner));
            Guard.ArgumentNotNull(readPipe, nameof(readPipe));
            Guard.ArgumentNotNull(writePipe, nameof(writePipe));

            Logger = typeResolver.GetLogger(GetType(), uniqueInstance: true);
            Owner = owner;
            ReadPipe = readPipe;
            WritePipe = writePipe;
            BinarySerializer = typeResolver.CreateSingleton<IBinarySerializer>();
        }

        protected override void OnDispose()
        {
            Logger.Info?.Trace("OnDispose");
            ReadPipe.Dispose();
            WritePipe.Dispose();
            Shutdown1ReceivedEvent.Set();
            Shutdown2ReceivedEvent.Set();
            Owner.OnRemoteEndLost("IpcConnector disposed");
            base.OnDispose();
            Logger.Dispose();
        }

        protected override async Task OnTeardownAsync(CancellationToken ct)
        {
            Logger.Info?.Trace("OnTeardownAsync");
            if (!Shutdown1ReceivedEvent.IsSet)
            {
                SendCode(InterprocessFrameType.Teardown1);
                await Shutdown2ReceivedEvent.WaitAsync(ct);
                await base.OnTeardownAsync(ct);
            }

            if (Shutdown2ReceivedEvent.IsSet)
            {
                Dispose();
                return;
            }

            SendCode(InterprocessFrameType.Teardown2);
            await base.OnTeardownAsync(ct);
        }

        protected abstract Task DoInitialize();

        internal async Task InitializeAsync(CancellationToken ct)
        {
            Logger.Info?.Trace("InitializeAsync");
            await DoInitialize();
            ReadLoop().FireAndForget();
            Logger.Info?.Trace("InitializeAsync completed");
        }

        private async Task ReadLoop()
        {
            try
            {
                while (true)
                {
                    using (var frame = await ReadPipe.GetNextFrame())
                    {
                        var code = (InterprocessFrameType)frame.Stream.ReadByte();
                        switch (code)
                        {
                            case InterprocessFrameType.IpcMessage:
                                var msg = BinarySerializer.Deserialize<WrappedInterprocessMessage>(frame.Stream);
                                Owner.OnMessageReceived(msg);
                                break;
                            case InterprocessFrameType.Teardown1:
                                Shutdown1ReceivedEvent.Set();
                                Owner.OnTeardownRequestReceived();
                                break;
                            case InterprocessFrameType.Teardown2:
                                Shutdown2ReceivedEvent.Set();
                                return;
                        }
                    }
                }
            }
            catch(EndOfStreamException ex)
            {
                Owner.OnRemoteEndLost("The IPC stream was closed", ex);
            }
            catch(Exception ex)
            {
                Owner.OnRemoteEndLost("Exception in IpcConnector::ReadLoop", ex);
            }
            finally
            {
                Dispose();
            }
        }

        public void ForwardMessage(IInterprocessMessage msg)
        {
            if(!(msg is WrappedInterprocessMessage wrapped))
            {
                wrapped = WrappedInterprocessMessage.Wrap(msg, BinarySerializer);
            }

            var stream = BinarySerializer.Serialize(wrapped, lengthPrefix: false, startOffset: 5);
            stream.Position = 0;
            int streamLen = checked((int)(stream.Length - 4));
            stream.WriteByte((byte)streamLen);
            stream.WriteByte((byte)(streamLen >> 8));
            stream.WriteByte((byte)(streamLen >> 16));
            stream.WriteByte((byte)(streamLen >> 24));
            stream.WriteByte((byte)InterprocessFrameType.IpcMessage);
            stream.Position = 0;
            WritePipe.WriteFrame(new LengthPrefixedStream(stream));
        }

        protected void SendCode(InterprocessFrameType code)
        {
            var ms = new MemoryStream(new byte[] { 1, 0, 0, 0, (byte)code });
            WritePipe.WriteFrame(new LengthPrefixedStream(ms));
        }

        protected async ValueTask ReceiveCode(InterprocessFrameType expectedCode)
        {
            using (var frame = await ReadPipe.GetNextFrame())
            {
                if (frame.StreamLength != 1)
                    throw new SerializationException($"Expected a single frame code, received {frame.StreamLength} bytes");

                var actualCode = (InterprocessFrameType)frame.Stream.ReadByte();
                if (actualCode != expectedCode)
                    throw new SerializationException($"Expected a {expectedCode} frame, received {actualCode}");
            }
        }
    }
}

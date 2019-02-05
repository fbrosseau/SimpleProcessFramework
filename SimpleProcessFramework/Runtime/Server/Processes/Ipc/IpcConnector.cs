using Spfx.Utilities;
using Spfx.Io;
using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities.Threading;
using System.IO;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace Spfx.Runtime.Server.Processes
{
    internal class SubprocessIpcConnector : IpcConnector, ISubprocessConnector
    {
        public SubprocessIpcConnector(ProcessContainer owner, ILengthPrefixedStreamReader readStream, ILengthPrefixedStreamWriter writeStream, IBinarySerializer serializer)
            : base(owner, readStream, writeStream, serializer)
        {
        }

        public Task<IInterprocessClientChannel> GetClientInfo(string uniqueId)
        {
            throw new NotImplementedException();
        }

        protected override async Task DoInitialize()
        {
            SendCode(InterprocessFrameType.Handshake1);
            await ReceiveCode(InterprocessFrameType.Handshake2);
            await Owner.CompleteInitialization();
            SendCode(InterprocessFrameType.Handshake3);
        }

        void IMessageCallbackChannel.SendBackMessage(string connectionId, IInterprocessMessage msg)
        {
            var wrapped = WrappedInterprocessMessage.Wrap(msg, BinarySerializer);
            wrapped.SourceConnectionId = connectionId;
            ForwardMessage(wrapped);
        }
    }

    internal class MasterProcessIpcConnector : IpcConnector
    {
        public MasterProcessIpcConnector(GenericChildProcessHandle owner, IProcessSpawnPunchHandles remoteProcessHandles, IBinarySerializer serializer)
            : base(
                  owner,
                  new SyncLengthPrefixedStreamReader(remoteProcessHandles.ReadStream, owner.ProcessUniqueId + " - MasterRead"),
                  new SyncLengthPrefixedStreamWriter(remoteProcessHandles.WriteStream, owner.ProcessUniqueId + " - MasterWrite"),
                  serializer)
        {
        }

        protected override async Task DoInitialize()
        {
            await ReceiveCode(InterprocessFrameType.Handshake1);
            await Owner.CompleteInitialization();
            SendCode(InterprocessFrameType.Handshake2);
            await ReceiveCode(InterprocessFrameType.Handshake3);
        }
    }

    internal interface IIpcConnectorListener
    {
        Task CompleteInitialization();
        void OnMessageReceived(WrappedInterprocessMessage msg);
        void OnTeardownRequestReceived();
    }

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


        protected IIpcConnectorListener Owner { get; }
        protected ILengthPrefixedStreamReader ReadPipe { get; }
        protected ILengthPrefixedStreamWriter WritePipe { get; }
        protected IBinarySerializer BinarySerializer { get; }

        protected AsyncManualResetEvent Shutdown1ReceivedEvent { get; } = new AsyncManualResetEvent();
        protected AsyncManualResetEvent Shutdown2ReceivedEvent { get; } = new AsyncManualResetEvent();

        protected IpcConnector(IIpcConnectorListener owner, ILengthPrefixedStreamReader readPipe, ILengthPrefixedStreamWriter writePipe, IBinarySerializer serializer)
        {
            Guard.ArgumentNotNull(owner, nameof(owner));
            Guard.ArgumentNotNull(readPipe, nameof(readPipe));
            Guard.ArgumentNotNull(writePipe, nameof(writePipe));

            Owner = owner;
            ReadPipe = readPipe;
            WritePipe = writePipe;
            BinarySerializer = serializer;
        }

        protected async Task<T> ReceiveMessage<T>()
            where T : IIpcFrame
        {
            using (var rawFrame = await ReadPipe.GetNextFrame())
            {
                var rawMsg = s_binarySerializer.Deserialize<IIpcFrame>(rawFrame.Stream);
                if (!(rawMsg is T t))
                    throw new SerializationException("Unexpected frame when waiting for " + typeof(T).FullName);

                return t;
            }
        }

        protected override void OnDispose()
        {
            ReadPipe.Dispose();
            WritePipe.Dispose();
            Shutdown1ReceivedEvent.Set();
            Shutdown2ReceivedEvent.Set();
            base.OnDispose();
        }

        protected async override Task OnTeardownAsync(CancellationToken ct)
        {
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

        internal async Task InitializeAsync()
        {
            await DoInitialize();
            ReadLoop().FireAndForget();
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
            catch(Exception)
            {
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

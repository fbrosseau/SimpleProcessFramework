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
            Handshake1 = -10000,
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

        protected ILogger Logger { get; }
        protected IIpcConnectorListener Owner { get; }
        protected ILengthPrefixedStreamReader ReadPipe { get; }
        protected ILengthPrefixedStreamWriter WritePipe { get; }
        protected IBinarySerializer BinarySerializer { get; }

        protected AsyncManualResetEvent Shutdown1ReceivedEvent { get; } = new AsyncManualResetEvent();
        protected AsyncManualResetEvent Shutdown2ReceivedEvent { get; } = new AsyncManualResetEvent();

        protected IpcConnector(IIpcConnectorListener owner, ILengthPrefixedStreamReader readPipe, ILengthPrefixedStreamWriter writePipe, ITypeResolver typeResolver, string remoteProcessId)
        {
            Guard.ArgumentNotNull(owner, nameof(owner));
            Guard.ArgumentNotNull(readPipe, nameof(readPipe));
            Guard.ArgumentNotNull(writePipe, nameof(writePipe));

            Logger = typeResolver.GetLogger(GetType(), uniqueInstance: true, remoteProcessId);
            Owner = owner;
            ReadPipe = readPipe;
            WritePipe = writePipe;
            WritePipe.WriteException += OnWriterExceptionCaught;
            BinarySerializer = typeResolver.CreateSingleton<IBinarySerializer>();
        }

        protected override void OnDispose()
        {
            Logger.Info?.Trace("OnDispose");
            ReadPipe.Dispose();
            WritePipe.WriteException -= OnWriterExceptionCaught;
            WritePipe.Dispose();
            Shutdown1ReceivedEvent.Set();
            Shutdown2ReceivedEvent.Set();
            RaiseFinalDisconnect("IpcConnector disposed");
            base.OnDispose();
            Logger.Dispose();
        }

        protected override async ValueTask OnTeardownAsync(CancellationToken ct)
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

        public async Task InitializeAsync(CancellationToken ct)
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
                    using var frame = await ReadPipe.GetNextFrame().ConfigureAwait(false);

                    if (frame.Code != null)
                    {
                        var code = (InterprocessFrameType)frame.Code;
                        Logger.Debug?.Trace("Received code " + code);

                        switch (code)
                        {
                            case InterprocessFrameType.Teardown1:
                                Shutdown1ReceivedEvent.Set();
                                Owner.OnTeardownRequestReceived();
                                break;
                            case InterprocessFrameType.Teardown2:
                                Shutdown2ReceivedEvent.Set();
                                return;
                        }
                    }
                    else
                    {
                        using var stream = frame.AcquireData();
                        var len = stream.Length;
                        var msg = BinarySerializer.Deserialize<WrappedInterprocessMessage>(stream);
                        Logger.Debug?.Trace($"Recv {msg.GetTinySummaryString()} ({stream.Length} bytes)");
                        Owner.OnMessageReceived(msg);
                    }
                }
            }
            catch (EndOfStreamException ex)
            {
                RaiseFinalDisconnect("The IPC stream was closed", ex);
            }
            catch (Exception ex)
            {
                RaiseFinalDisconnect("Exception in IpcConnector::ReadLoop", ex);
            }
            finally
            {
                Dispose();
            }
        }

        private void OnWriterExceptionCaught(object sender, StreamWriterExceptionEventArgs e)
        {
            RaiseFinalDisconnect("An exception occured writing to the remote process", e.CaughtException);
        }

        private void RaiseFinalDisconnect(string description, Exception caughtException = null)
        {
            Logger.Debug?.Trace(caughtException, "RaiseFinalDisconnect: " + description);
            Owner.OnRemoteEndLost(description, caughtException);
        }

        public void ForwardMessage(IInterprocessMessage msg)
        {
            if (!(msg is WrappedInterprocessMessage wrapped))
            {
                wrapped = WrappedInterprocessMessage.Wrap(msg, BinarySerializer);
            }

            var stream = BinarySerializer.Serialize(wrapped, lengthPrefix: true);
            Logger.Debug?.Trace($"Sending out {msg.GetTinySummaryString()} ({stream.Length} bytes)");
            WritePipe.WriteFrame(PendingWriteFrame.CreateFromFramedData(stream));
        }

        protected void SendCode(InterprocessFrameType code)
        {
            Logger.Debug?.Trace("Sending code " + code);
            WritePipe.WriteFrame(PendingWriteFrame.CreateCodeFrame((int)code));
        }

        protected async ValueTask ReceiveCode(InterprocessFrameType expectedCode)
        {
            Logger.Debug?.Trace("Expecting code " + expectedCode);

            using var frame = await ReadPipe.GetNextFrame();

            if (frame.Code is null)
                throw new SerializationException($"Expected a single frame code");

            var actualCode = (InterprocessFrameType)frame.Code;
            if (actualCode != expectedCode)
                throw new SerializationException($"Expected a {expectedCode} frame, received {actualCode}");
        }
    }
}

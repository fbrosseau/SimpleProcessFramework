using SimpleProcessFramework.Runtime.Client;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Serialization;
using SimpleProcessFramework.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Client
{
    [DataContract]
    public class RemoteClientConnectionRequest
    {
        [DataMember]
        public byte[] Salt { get; }

        [DataMember]
        public byte[] Secret { get; }
    }

    [DataContract]
    public class RemoteClientConnectionResponse
    {
        [DataMember]
        public bool Success { get; set; }

        [DataMember]
        public string Error { get; set; }
    }

    internal class RemoteInterprocessConnection : AbstractInterprocessConection
    {
        private readonly EndPoint m_destination;

        public RemoteInterprocessConnection(EndPoint destination, IBinarySerializer serializer)
            : base(serializer)
        {
            m_destination = destination;
        }

        internal override async Task<(Stream readStream, Stream writeStream)> ConnectStreamsAsync()
        {
            using (var disposeBag = new DisposeBag())
            {
                var client = new Socket(SocketType.Stream, ProtocolType.Tcp);
                disposeBag.Add(client);

                await Task.Factory.FromAsync((cb, s) => client.BeginConnect(m_destination, cb, s), client.EndConnect, null);

                var ns = disposeBag.Add(new NetworkStream(client));
                var tlsStream = disposeBag.Add(new SslStream(ns, false, delegate { return true; }));

                var timeout = Task.Delay(TimeSpan.FromSeconds(30));
                var auth = Authenticate(tlsStream);

                var winner = await Task.WhenAny(timeout, auth);

                if (ReferenceEquals(winner, timeout))
                    throw new TimeoutException("Authentication timed out");
                
                disposeBag.ReleaseAll();
                return (tlsStream, tlsStream);
            }
        }

        private async Task Authenticate(SslStream tlsStream)
        {
            await tlsStream.AuthenticateAsClientAsync("unused", null, SslProtocols.None, false);

            var hello = BinarySerializer.Serialize<object>(new RemoteClientConnectionRequest(), lengthPrefix: true);
            await hello.CopyToAsync(tlsStream);

            var responseStream = await tlsStream.ReadLengthPrefixedBlock();
            var response = (RemoteClientConnectionResponse)BinarySerializer.Deserialize<object>(responseStream);

            if (response.Success)
                return;

            throw new RemoteConnectionException(string.IsNullOrWhiteSpace(response.Error) ? "The connection was refused by the remote host" : response.Error);
        }
    }

    internal abstract class AbstractInterprocessConection : IInterprocessConnection
    {
        private readonly AsyncQueue<PendingOperation> m_pendingWrites;
        private Task m_writeFlow;
        private Task m_readFlow;
        private Task m_keepAliveTask;

        protected Stream ReadStream { get; private set; }
        protected Stream WriteStream { get; private set; }
        protected IBinarySerializer BinarySerializer { get; }
        protected virtual TimeSpan KeepAliveInterval { get; } = TimeSpan.FromSeconds(30);

        private class PendingOperation : IAbortableItem
        {
            public Stream Data { get; }
            public TaskCompletionSource<object> Completion { get; }
            public IInterprocessRequest Request { get; }

            public PendingOperation(IInterprocessRequest req, Stream serializedRequest)
            {
                Request = req;
                Completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                Data = serializedRequest;
            }

            public PendingOperation(ConnectionCodes keepAlive)
            {
                Data = new MemoryStream(BitConverter.GetBytes((int)keepAlive));
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

        private Dictionary<long, PendingOperation> m_pendingResponses = new Dictionary<long, PendingOperation>();

        private async ValueTask DoWriteAsync(PendingOperation op)
        {
            try
            {
                try
                {
                    if (op.Request.ExpectResponse)
                    {
                        lock (m_pendingResponses)
                        {
                            m_pendingResponses.Add(op.Request.CallId, op);
                        }

                        op.Completion.Task.ContinueWith(t =>
                        {
                            lock (m_pendingResponses)
                            {
                                m_pendingResponses.Remove(op.Request.CallId);
                            }
                        }).FireAndForget();
                    }

                    await op.Data.CopyToAsync(WriteStream);

                    if (!op.Request.ExpectResponse)
                    {
                        op.Completion.TrySetResult(null);
                        op.Dispose();
                    }
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

        private void HandleFailure(Exception ex)
        {
            m_pendingWrites.Dispose(ex);
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

                m_writeFlow = m_pendingWrites.ForEachAsync(i => DoWriteAsync(i));

                await ReceiveLoop();
            }
            catch (Exception ex)
            {
                HandleFailure(ex);
            }
        }

        private async Task ReceiveLoop()
        {
            while (true)
            {
                var stream = await ReadStream.ReadLengthPrefixedBlock();
                var msg = BinarySerializer.Deserialize<IInterprocessRequest>(stream);
                switch (msg)
                {
                    case RemoteInvocationResponse callResponse:
                        PendingOperation op;
                        lock (m_pendingResponses)
                        {
                            m_pendingResponses.TryGetValue(callResponse.CallId, out op);
                        }

                        if (op is null)
                            continue;

                        if (callResponse is RemoteCallSuccessResponse success)
                        {
                            op.Completion.TrySetResult(success.Result);
                        }
                        else if (callResponse is RemoteCallFailureResponse failure)
                        {
                            op.Completion.TrySetException(failure.Error);
                        }
                        else
                        {
                            HandleFailure(new SerializationException("Unexpected response"));
                        }

                        break;
                }
            }
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
            await EnqueueOperation(new PendingOperation(ConnectionCodes.KeepAlive));
            RescheduleKeepAlive();
        }

        internal enum ConnectionCodes
        {
            KeepAlive = -1
        }

        internal abstract Task<(Stream readStream, Stream writeStream)> ConnectStreamsAsync();

        public Task<object> SendRequest(IInterprocessRequest req)
        {
            var serializedRequest = BinarySerializer.Serialize(req, lengthPrefix: true);
            return EnqueueOperation(new PendingOperation(req, serializedRequest));
        }

        private Task<object> EnqueueOperation(PendingOperation pendingOperation)
        {
            m_pendingWrites.Enqueue(pendingOperation);
            return pendingOperation.Completion.Task;
        }
    }
}
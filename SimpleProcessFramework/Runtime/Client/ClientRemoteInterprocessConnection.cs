using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Common;
using SimpleProcessFramework.Runtime.Exceptions;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Runtime.Server;
using SimpleProcessFramework.Serialization;
using SimpleProcessFramework.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Client
{
    internal class ClientSameClusterInterprocessConnection : IClientInterprocessConnection, IInterprocessClientChannel
    {
        public event EventHandler ConnectionLost;

        private readonly IInternalMessageDispatcher m_localProcess;
        private readonly IInterprocessClientProxy m_proxyToThis;
        private readonly IBinarySerializer m_binarySerializer;
        private readonly SimpleUniqueIdFactory<PendingClientCall> m_pendingRequests = new SimpleUniqueIdFactory<PendingClientCall>();
        private readonly SimpleUniqueIdFactory<Action<EventRaisedMessage>> m_eventRegistrations = new SimpleUniqueIdFactory<Action<EventRaisedMessage>>();

        public long UniqueId => long.MaxValue;
        public IInterprocessClientProxy GetWrapperProxy() => m_proxyToThis;

        private class PendingClientCall : TaskCompletionSource<object>
        {
            public IInterprocessRequest Request { get; }

            public PendingClientCall(IInterprocessRequest req)
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
                Request = req;
            }
        }

        public ClientSameClusterInterprocessConnection(ITypeResolver typeResolver)
        {
            m_localProcess = typeResolver.GetSingleton<IInternalMessageDispatcher>();
            m_proxyToThis = new ConcreteClientProxy(this);
            m_binarySerializer = typeResolver.GetSingleton<IBinarySerializer>();

            typeResolver.GetSingleton<IClientConnectionManager>().RegisterClientChannel(this);
        }

        Task IClientInterprocessConnection.ChangeEventSubscription(EventRegistrationRequestInfo req)
        {
            var outgoingMessage = new EventRegistrationRequest
            {
                Destination = req.Destination,
                RemovedEvents = req.RemovedEvents?.ToList()
            };

            if (req.NewEvents?.Count > 0)
            {
                outgoingMessage.AddedEvents = new List<EventRegistrationItem>();

                foreach (var evt in req.NewEvents)
                {
                    outgoingMessage.AddedEvents.Add(new EventRegistrationItem
                    {
                        EventName = evt.EventName,
                        RegistrationId = m_eventRegistrations.GetNextId(evt.Callback)
                    });
                }
            }

            return ((IClientInterprocessConnection)this).SerializeAndSendMessage(outgoingMessage);
        }

        public void Dispose()
        {
        }

        public ValueTask<ProcessEndpointMethodDescriptor> GetRemoteMethodDescriptor(ProcessEndpointAddress destination, ReflectedMethodInfo calledMethod)
        {
            throw new NotImplementedException();
        }

        void IInterprocessConnection.Initialize()
        {
            // TODO - Bad code
        }

        void IInterprocessClientChannel.Initialize(IClientRequestHandler clientConnectionsManager)
        {
            // TODO - Bad code
        }

        void IInterprocessClientChannel.SendMessage(IInterprocessMessage msg)
        {
            if (msg is WrappedInterprocessMessage wrapped)
                msg = wrapped.Unwrap(m_binarySerializer);

            switch (msg)
            {
                case RemoteInvocationResponse callResponse:
                    var completion = m_pendingRequests.RemoveById(callResponse.CallId);
                    callResponse.ForwardResult(completion);
                    break;
                case EventRaisedMessage eventMsg:
                    var handler = m_eventRegistrations.TryGetById(eventMsg.SubscriptionId);
                    handler?.Invoke(eventMsg);
                    break;
                default:
                    throw new InvalidOperationException("Message of type " + msg.GetType().FullName + " is not handled");
            }

            if (msg is RemoteInvocationResponse resp)
            {
                var completion = m_pendingRequests.RemoveById(resp.CallId);
                resp.ForwardResult(completion);
            }
        }

        Task<object> IInterprocessConnection.SerializeAndSendMessage(IInterprocessMessage msg, CancellationToken ct)
        {
            Task<object> completion = BoxHelper.GetDefaultSuccessTask<object>();

            if (msg is IInterprocessRequest req && req.ExpectResponse)
            {
                var tcs = new PendingClientCall(req);
                req.CallId = m_pendingRequests.GetNextId(tcs);
                completion = tcs.Task;
            }

            m_localProcess.ForwardOutgoingMessage(this, msg, ct);
            return completion;
        }
    }

    internal class ClientRemoteInterprocessConnection : AbstractClientInterprocessConnection
    {
        private readonly EndPoint m_destination;

        public ClientRemoteInterprocessConnection(EndPoint destination, IBinarySerializer serializer)
            : base(serializer)
        {
            m_destination = destination;
        }

        protected override async Task<ProcessEndpointDescriptor> GetRemoteEndpointMetadata(ProcessEndpointAddress destination, ReflectedTypeInfo type)
        {
            return (ProcessEndpointDescriptor)await SerializeAndSendMessage(new EndpointDescriptionRequest
            {
                Destination = destination
            });
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
}
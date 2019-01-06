using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Common;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Serialization;
using SimpleProcessFramework.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Client
{
    public interface IClientInterprocessConnection : IInterprocessConnection
    {
        ValueTask<ProcessEndpointMethodDescriptor> GetRemoteMethodDescriptor(ProcessEndpointAddress destination, ReflectedMethodInfo calledMethod);
    }

    internal abstract class AbstractClientInterprocessConnection : AbstractInterprocessConection, IClientInterprocessConnection
    {
        private Dictionary<long, PendingOperation> m_pendingResponses = new Dictionary<long, PendingOperation>();

        protected AbstractClientInterprocessConnection(IBinarySerializer serializer)
            : base(serializer)
        {
        }

        protected override void HandleMessage(IInterprocessMessage msg)
        {
            msg = ((WrappedInterprocessMessage)msg).Unwrap(BinarySerializer);
            switch (msg)
            {
                case RemoteInvocationResponse callResponse:
                    PendingOperation op;
                    lock (m_pendingResponses)
                    {
                        m_pendingResponses.TryGetValue(callResponse.CallId, out op);
                    }

                    if (op is null)
                        return;

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
                default:
                    base.HandleMessage(msg);
                    break;
            }
        }

        protected override async ValueTask DoWrite(PendingOperation op)
        {
            bool expectResponse = false;

            if (op.Request is IInterprocessRequest req && req.ExpectResponse)
            {
                expectResponse = true;
                lock (m_pendingResponses)
                {
                    m_pendingResponses.Add(req.CallId, op);
                }

                op.Completion.Task.ContinueWith(t =>
                {
                    lock (m_pendingResponses)
                    {
                        m_pendingResponses.Remove(req.CallId);
                    }
                }).FireAndForget();
            }

            await base.DoWrite(op).ConfigureAwait(false);

            if (!expectResponse)
            {
                op.Completion.TrySetResult(null);
                op.Dispose();
            }
        }

        private readonly Dictionary<ProcessEndpointAddress, DescribedRemoteEndpoint> m_knownRemoteEndpoints = new Dictionary<ProcessEndpointAddress, DescribedRemoteEndpoint>();

        private class DescribedRemoteEndpoint
        {
            public Dictionary<ReflectedMethodInfo, ProcessEndpointMethodDescriptor> RemoteMethods;
        }

        public async ValueTask<ProcessEndpointMethodDescriptor> GetRemoteMethodDescriptor(ProcessEndpointAddress destination, ReflectedMethodInfo calledMethod)
        {
            DescribedRemoteEndpoint remoteDescription;
            lock (m_knownRemoteEndpoints)
            {
                m_knownRemoteEndpoints.TryGetValue(destination, out remoteDescription);
            }

            if (remoteDescription is null)
            {
                var rawDescriptor = await GetRemoteEndpointMetadata(destination, calledMethod.Type);
                remoteDescription = new DescribedRemoteEndpoint
                {
                    RemoteMethods = rawDescriptor.Methods.ToDictionary(m => m.Method)
                };

                lock (m_knownRemoteEndpoints)
                {
                    m_knownRemoteEndpoints[destination] = remoteDescription;
                }
            }

            return remoteDescription.RemoteMethods[calledMethod];
        }

        protected abstract Task<ProcessEndpointDescriptor> GetRemoteEndpointMetadata(ProcessEndpointAddress destination, ReflectedTypeInfo type);
    }
}
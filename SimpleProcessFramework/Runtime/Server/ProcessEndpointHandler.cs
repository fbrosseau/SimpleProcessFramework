using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace SimpleProcessFramework.Runtime.Server
{
    [DataContract]
    public abstract class ProcessEndpointHandler : IProcessEndpointHandler
    {
        private object m_realTarget;
        private ProcessEndpointDescriptor m_descriptor;
        private readonly Dictionary<PendingCallKey, IInterprocessRequestContext> m_pendingCalls = new Dictionary<PendingCallKey, IInterprocessRequestContext>();
        private ProcessEndpointMethodDescriptor[] m_methods;

        protected ProcessEndpointHandler(object realTarget, ProcessEndpointDescriptor descriptor)
        {
            m_realTarget = realTarget;
            m_descriptor = descriptor;
            m_methods = m_descriptor.Methods.ToArray();
        }

        public virtual void HandleMessage(IInterprocessRequestContext req)
        {
            try
            {
                switch (req.Request)
                {
                    case EndpointDescriptionRequest _:
                        req.Respond(m_descriptor);
                        break;
                    case RemoteCallRequest call:
                        DoRemoteCall(req, call);
                        break;
                    case RemoteCallCancellationRequest _:
                        CancelCall(req);
                        break;
                    default:
                        req.Fail(new InvalidOperationException("Cannot handle request"));
                        break;
                }
            }
            catch(Exception ex)
            {
                req.Fail(ex);
            }
        }

        private void CancelCall(IInterprocessRequestContext req)
        {
            var key = new PendingCallKey(req);
            IInterprocessRequestContext existingCall;

            lock(m_pendingCalls)
            {
                // we don't actually remove the call here, the only path that removes calls is the actual completion/failure.
                m_pendingCalls.TryGetValue(key, out existingCall);
            }

            existingCall?.Cancel();
        }

        private void DoRemoteCall(IInterprocessRequestContext req, RemoteCallRequest remoteCall)
        {
            var key = new PendingCallKey(req);
            lock (m_pendingCalls)
            {
                m_pendingCalls.Add(key, req);
            }

            if (remoteCall.MethodId < 0 || remoteCall.MethodId >= m_methods.Length)
                ThrowBadInvocation();

            var method = m_methods[remoteCall.MethodId];
            var args = remoteCall.GetArgsOrEmpty();

            if (args.Length != method.Method.Arguments.Length)
                ThrowBadInvocation();

            if (method.IsCancellable)
                req.SetupCancellationToken();

            DoRemoteCallImpl(req);
        }

        protected void ThrowBadInvocation()
        {
            throw new InvalidOperationException("Unknown method");
        }

        internal void CompleteCall(IInterprocessRequestContext req)
        {
            var key = new PendingCallKey(req);
            lock (m_pendingCalls)
            {
                m_pendingCalls.Remove(key);
            }
        }

        protected abstract void DoRemoteCallImpl(IInterprocessRequestContext callRequest); // codegen

        #region Inner classes
        internal static class Reflection
        {
            public static MethodInfo DoRemoteCallImplMethod => typeof(ProcessEndpointHandler).FindUniqueMethod(nameof(DoRemoteCallImpl));
            public static MethodInfo ThrowBadInvocationMethod => typeof(ProcessEndpointHandler).FindUniqueMethod(nameof(ThrowBadInvocation));
        }

        private struct PendingCallKey : IEquatable<PendingCallKey>
        {
            public IInterprocessClientContext Client { get; }
            public long CallId { get; }

            public PendingCallKey(IInterprocessRequestContext req)
                : this(req.Client, req.Request.CallId)
            {
            }

            public PendingCallKey(IInterprocessClientContext client, long callId)
            {
                Client = client;
                CallId = callId;
            }

            public bool Equals(PendingCallKey other)
            {
                return other.CallId == CallId && ReferenceEquals(Client, other.Client);
            }

            public override int GetHashCode()
            {
                return RuntimeHelpers.GetHashCode(Client) ^ CallId.GetHashCode();
            }

            public override bool Equals(object obj) => (obj as PendingCallKey?)?.Equals(this) ?? false;
        }
        #endregion
    }
}
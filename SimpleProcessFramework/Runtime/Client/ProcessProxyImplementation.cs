using Oopi.Utilities;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Common;
using SimpleProcessFramework.Runtime.Messages;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Client
{
    internal sealed class RemoteCallCompletion<T> : TaskCompletionSource<T>
    {
        public long Id { get; }
        private static long s_nextId;

        public RemoteCallCompletion()
            : base(TaskCreationOptions.RunContinuationsAsynchronously)
        {
            Id = Interlocked.Increment(ref s_nextId);
        }
    }

    public abstract class ProcessProxyImplementation
    {
        private IInterprocessRequestHandler m_handler;
        private IClientInterprocessConnection m_connection;
        private ProcessEndpointAddress m_remoteAddress;
        private TimeSpan m_callTimeout;

        internal void Initialize(IInterprocessRequestHandler handler, IClientInterprocessConnection connection, ProcessEndpointAddress remoteAddress)
        {
            m_handler = handler;
            m_connection = connection;
            m_remoteAddress = remoteAddress;
        }

        protected Task WrapTaskReturn(object[] args, ReflectedMethodInfo method, CancellationToken ct)
        {
            return WrapTaskOfTReturn<VoidType>(args, method, ct);
        }

        protected ValueTask WrapValueTaskReturn(object[] args, ReflectedMethodInfo method, CancellationToken ct)
        {
            return new ValueTask(WrapTaskOfTReturn<VoidType>(args, method, ct));
        }

        protected Task<T> WrapTaskOfTReturn<T>(object[] args, ReflectedMethodInfo method, CancellationToken ct)
        {
            return ExecuteRequest<T>(method, new RemoteCallRequest
            {
                Arguments = args
            }, ct);
        }

        protected ValueTask<T> WrapValueTaskOfTReturn<T>(object[] args, ReflectedMethodInfo method, CancellationToken ct)
        {
            return new ValueTask<T>(WrapTaskOfTReturn<T>(args, method, ct));
        }

        private async Task<T> ExecuteRequest<T>(ReflectedMethodInfo calledMethod, RemoteCallRequest remoteCallRequest, CancellationToken ct)
        {
            remoteCallRequest.Destination = m_remoteAddress;
            remoteCallRequest.AbsoluteTimeout = m_callTimeout;
            remoteCallRequest.Cancellable = ct.CanBeCanceled || remoteCallRequest.HasTimeout;
            remoteCallRequest.MethodId = (await m_connection.GetRemoteMethodDescriptor(m_remoteAddress, calledMethod)).MethodId;

            var res = await m_handler.ProcessCall(m_connection, remoteCallRequest, ct).ConfigureAwait(false);

            if (typeof(T) == typeof(VoidType))
            {
                return default;
            }
            else
            {
                return (T)res;
            }
        }

        internal static class Reflection
        {
            public static MethodInfo WrapTaskReturnMethod => typeof(ProcessProxyImplementation).FindUniqueMethod(nameof(WrapTaskReturn));
            public static MethodInfo WrapValueTaskReturnMethod => typeof(ProcessProxyImplementation).FindUniqueMethod(nameof(WrapTaskReturnMethod));
            public static MethodInfo GetWrapTaskOfTReturnMethod(Type t) => typeof(ProcessProxyImplementation).FindUniqueMethod(nameof(WrapTaskOfTReturn)).MakeGenericMethod(t);
            public static MethodInfo GetWrapValueTaskOfTReturnMethod(Type t) => typeof(ProcessProxyImplementation).FindUniqueMethod(nameof(WrapValueTaskReturnMethod)).MakeGenericMethod(t);
        }
    }
}

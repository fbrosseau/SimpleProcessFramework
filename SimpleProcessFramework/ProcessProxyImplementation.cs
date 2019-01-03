using Oopi.Utilities;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Messages;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework
{
    public class InvalidProxyInterfaceException : Exception
    {
        public InvalidProxyInterfaceException(string message) 
            : base(message)
        {
        }
    }

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

    public interface IProxiedCallHandler
    {
        Task<object> ProcessCall(RemoteInvocationRequest req, CancellationToken ct);
    }

    public abstract class ProcessProxyImplementation
    {
        private IProxiedCallHandler m_handler;
        private ProcessEndpointAddress m_remoteAddress;
        private TimeSpan m_callTimeout;

        internal void Initialize(IProxiedCallHandler handler, ProcessEndpointAddress remoteAddress)
        {
            m_handler = handler;
            m_remoteAddress = remoteAddress;
        }

        protected Task WrapTaskReturn(object[] args, ReflectedMethodInfo method, CancellationToken ct)
        {
            return WrapGenericTaskReturn<VoidType>(args, method, ct);
        }

        protected Task<T> WrapGenericTaskReturn<T>(object[] args, ReflectedMethodInfo method, CancellationToken ct)
        {
            return ExecuteRequest<T>(new RemoteCallRequest
            {
                Arguments = args
            }, ct);
        }

        private async Task<T> ExecuteRequest<T>(RemoteCallRequest remoteCallRequest, CancellationToken ct)
        {
            remoteCallRequest.Destination = m_remoteAddress;
            remoteCallRequest.AbsoluteTimeout = m_callTimeout;
            remoteCallRequest.Cancellable = ct.CanBeCanceled || remoteCallRequest.HasTimeout;

            var res = await m_handler.ProcessCall(remoteCallRequest, ct).ConfigureAwait(false);

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
            public static MethodInfo WrapTaskReturnMethod { get; } = typeof(ProcessProxyImplementation).FindUniqueMethod(nameof(WrapTaskReturn));
            public static MethodInfo WrapGenericTaskReturnMethod { get; } = typeof(ProcessProxyImplementation).FindUniqueMethod(nameof(WrapGenericTaskReturn));
        }
    }
}

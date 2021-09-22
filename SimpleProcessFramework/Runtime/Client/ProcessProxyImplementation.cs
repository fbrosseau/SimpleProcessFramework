using Spfx.Reflection;
using Spfx.Runtime.Client.Events;
using Spfx.Runtime.Messages;
using Spfx.Utilities;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client
{
    internal sealed class RemoteCallCompletion<T> : TaskCompletionSource<T>
    {
        public long Id { get; }

        public RemoteCallCompletion()
            : base(TaskCreationOptions.RunContinuationsAsynchronously)
        {
            Id = RemoteCallCompletion.GetNextCallId();
        }
    }

    internal static class RemoteCallCompletion
    {
        private static long s_nextId;

        public static long GetNextCallId()
        {
            return Interlocked.Increment(ref s_nextId);
        }
    }

    public abstract class ProcessProxyImplementation
    {
        internal IClientInterprocessConnection m_connection;
        internal TimeSpan CallTimeout { get; set; }

        private Dictionary<string, ProcessProxyEventSubscriptionInfo> m_eventDispatchMethods;
        internal ProcessEndpointAddress RemoteAddress { get; set; }
        internal ProcessProxy ParentProxy { get; private set; }

        internal void Initialize(ProcessProxy parentProxy, IClientInterprocessConnection connection, ProcessEndpointAddress remoteAddress)
        {
            m_connection = connection;
            RemoteAddress = remoteAddress;
            ParentProxy = parentProxy;
        }

        protected ProcessProxyEventSubscriptionInfo InitEventInfo(ReflectedEventInfo evt, ProcessProxyEventSubscriptionInfo.RaiseEventDelegate raiseEventCallback)
        {
            if (m_eventDispatchMethods is null)
                m_eventDispatchMethods = new Dictionary<string, ProcessProxyEventSubscriptionInfo>();

            var info = new ProcessProxyEventSubscriptionInfo(this, evt, raiseEventCallback);
            m_eventDispatchMethods.Add(evt.Name, info);
            return info;
        }

        protected Task WrapTaskReturn(object[] args, ReflectedMethodInfo method, CancellationToken ct)
        {
            return WrapTaskOfTReturn<object>(args, method, ct);
        }

        protected ValueTask WrapValueTaskReturn(object[] args, ReflectedMethodInfo method, CancellationToken ct)
        {
            return new ValueTask(WrapTaskOfTReturn<object>(args, method, ct));
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

        private Task<T> ExecuteRequest<T>(ReflectedMethodInfo calledMethod, RemoteInvocationRequest remoteCallRequest, CancellationToken ct)
        {
            remoteCallRequest.Destination = RemoteAddress;
            remoteCallRequest.AbsoluteTimeout = CallTimeout;
            remoteCallRequest.Cancellable = ct.CanBeCanceled || remoteCallRequest.HasTimeout;

            if (remoteCallRequest is RemoteCallRequest callReq)
            {
                callReq.MethodName = calledMethod.GetUniqueName();
                //callReq.MethodId = (await m_connection.GetRemoteMethodDescriptor(m_remoteAddress, calledMethod)).MethodId;
            }

            return m_connection.SerializeAndSendMessage<T>(remoteCallRequest, ct);
        }

        protected void AddEventSubscription(Delegate handler, ProcessProxyEventSubscriptionInfo eventInfo)
        {
            EventSubscriptionScope.AddEventSubscription(RemoteAddress, m_connection, handler, eventInfo);
        }

        protected void RemoveEventSubscription(Delegate handler, ProcessProxyEventSubscriptionInfo eventInfo)
        {
            EventSubscriptionScope.RemoveEventSubscription(RemoteAddress, m_connection, handler, eventInfo);
        }

        internal ValueTask PingAsync()
        {
            return new ValueTask(m_connection.SerializeAndSendMessage(new PingRequest
            {
                Destination = RemoteAddress,
            }));
        }

        internal static ProcessProxyImplementation Unwrap(object endpointInstance)
        {
            Guard.ArgumentNotNull(endpointInstance, nameof(endpointInstance));
            if (endpointInstance is ProcessProxyImplementation impl)
                return impl;

            throw new ArgumentException("The instance must be a valid proxy, not " + endpointInstance.GetType().AssemblyQualifiedName);
        }

        internal static class Reflection
        {
            public static MethodInfo WrapTaskReturnMethod => typeof(ProcessProxyImplementation).FindUniqueMethod(nameof(WrapTaskReturn));
            public static MethodInfo WrapValueTaskReturnMethod => typeof(ProcessProxyImplementation).FindUniqueMethod(nameof(WrapTaskReturnMethod));
            public static MethodInfo AddEventSubscriptionMethod => typeof(ProcessProxyImplementation).FindUniqueMethod(nameof(AddEventSubscription));
            public static MethodInfo RemoveEventSubscriptionMethod => typeof(ProcessProxyImplementation).FindUniqueMethod(nameof(RemoveEventSubscription));
            public static MethodInfo InitEventInfoMethod => typeof(ProcessProxyImplementation).FindUniqueMethod(nameof(InitEventInfo));
            public static ConstructorInfo Ctor => typeof(ProcessProxyImplementation).GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);

            public static MethodInfo GetWrapTaskOfTReturnMethod(Type t) => typeof(ProcessProxyImplementation).FindUniqueMethod(nameof(WrapTaskOfTReturn)).MakeGenericMethod(t);
            public static MethodInfo GetWrapValueTaskOfTReturnMethod(Type t) => typeof(ProcessProxyImplementation).FindUniqueMethod(nameof(WrapValueTaskReturnMethod)).MakeGenericMethod(t);

            internal static readonly ProcessProxyEventSubscriptionInfo.RaiseEventDelegate RaiseNonGenericEventAction = (sender, handler, args) =>
            {
                ((EventHandler)handler)?.Invoke(sender, (EventArgs)args);
            };

            public static class GenericHelper<T>
            {
                internal static readonly ProcessProxyEventSubscriptionInfo.RaiseEventDelegate RaiseGenericEventAction = (sender, handler, args) =>
                {
                    ((EventHandler<T>)handler)?.Invoke(sender, (T)args);
                };
            }

            public static FieldInfo EventState_NonGenericCallbackField => typeof(Reflection).GetField(nameof(RaiseNonGenericEventAction), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            internal static FieldInfo GetEventState_GenericCallbackField(Type eventArgsType)
            {
                var parentType = typeof(GenericHelper<>).MakeGenericType(eventArgsType);
                return parentType.GetField(nameof(GenericHelper<int>.RaiseGenericEventAction), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            }
        }
    }
}
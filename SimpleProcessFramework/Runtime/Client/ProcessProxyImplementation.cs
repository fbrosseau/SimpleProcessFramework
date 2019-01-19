﻿using Oopi.Utilities;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Common;
using SimpleProcessFramework.Runtime.Messages;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
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
        private IClientInterprocessConnection m_connection;
        private ProcessEndpointAddress m_remoteAddress;
        internal TimeSpan CallTimeout { get; set; }

        protected Dictionary<string, ProcessProxyEventSubscriptionInfo> EventDispatchMethods { get; } = new Dictionary<string, ProcessProxyEventSubscriptionInfo>();

        internal void Initialize(IClientInterprocessConnection connection, ProcessEndpointAddress remoteAddress)
        {
            m_connection = connection;
            m_remoteAddress = remoteAddress;
        }

        protected void InitEventInfo(ProcessProxyEventSubscriptionInfo evt)
        {
            EventDispatchMethods.Add(evt.Event.Name, evt);
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

        private async Task<T> ExecuteRequest<T>(ReflectedMethodInfo calledMethod, RemoteInvocationRequest remoteCallRequest, CancellationToken ct)
        {
            remoteCallRequest.Destination = m_remoteAddress;
            remoteCallRequest.AbsoluteTimeout = CallTimeout;
            remoteCallRequest.Cancellable = ct.CanBeCanceled || remoteCallRequest.HasTimeout;

            if (remoteCallRequest is RemoteCallRequest callReq)
            {
                callReq.MethodName = calledMethod.GetUniqueName();
                //callReq.MethodId = (await m_connection.GetRemoteMethodDescriptor(m_remoteAddress, calledMethod)).MethodId;
            }

            var res = await m_connection.SerializeAndSendMessage(remoteCallRequest, ct).ConfigureAwait(false);

            if (typeof(T) == typeof(VoidType))
            {
                return default;
            }
            else
            {
                return (T)res;
            }
        }

        internal void RaiseEvent(EventRaisedMessage msg)
        {
            if (!EventDispatchMethods.TryGetValue(msg.EventName, out var eventInfo))
                throw new NotSupportedException("This type has no events");

            eventInfo.RaiseEventCallback(this, eventInfo.EventHandler, msg.EventArgs);
        }

        protected void AddEventSubscription(Delegate handler, ProcessProxyEventSubscriptionInfo eventInfo)
        {
            bool add = false;
            lock (eventInfo)
            {
                add = eventInfo.EventHandler is null;
                eventInfo.EventHandler = Delegate.Combine(eventInfo.EventHandler, handler);
            }

            if (!add)
                return;

            m_connection.ChangeEventSubscription(new EventRegistrationRequestInfo(m_remoteAddress)
            {
                NewEvents = { new EventRegistrationRequestInfo.NewEventRegistration(eventInfo.Event.Name, RaiseEvent)}
            }).Wait();
        }

        protected void RemoveEventSubscription(Delegate handler, ProcessProxyEventSubscriptionInfo eventInfo)
        {
            bool delete = false;
            lock (eventInfo)
            {
                delete = eventInfo.EventHandler != null;
                eventInfo.EventHandler = Delegate.Remove(eventInfo.EventHandler, handler);
                delete &= eventInfo.EventHandler == null;
            }

            if (!delete)
                return;

            m_connection.ChangeEventSubscription(new EventRegistrationRequestInfo(m_remoteAddress)
            {
                RemovedEvents = { eventInfo.Event.Name }
            }).Wait();
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

            internal static readonly Action<ProcessProxyImplementation, Delegate, object> RaiseNonGenericEventAction = (sender, handler, args) =>
            {
                ((EventHandler)handler)?.Invoke(sender, (EventArgs)args);
            };

            public static class GenericHelper<T>
            {
                internal static readonly Action<ProcessProxyImplementation, Delegate, object> RaiseGenericEventAction = (sender, handler, args) =>
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

    public class ProcessProxyEventSubscriptionInfo
    {
        public ReflectedEventInfo Event { get; }
        public Action<ProcessProxyImplementation, Delegate, object> RaiseEventCallback { get; }
        public Delegate EventHandler { get; internal set; }

        public ProcessProxyEventSubscriptionInfo(ReflectedEventInfo evt, Action<ProcessProxyImplementation, Delegate, object> raiseEventCallback)
        {
            Event = evt;
            RaiseEventCallback = raiseEventCallback;
        }
    }
}
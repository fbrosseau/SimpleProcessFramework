using Spfx.Reflection;
using System;

namespace Spfx.Runtime.Client.Eventing
{
    public class ProcessProxyEventSubscriptionInfo
    {
        public delegate void RaiseEventDelegate(ProcessProxyImplementation instance, Delegate del, object args);

        private readonly ProcessProxyImplementation m_owner;

        public ReflectedEventInfo Event { get; }
        public RaiseEventDelegate RaiseEventCallback { get; }
        public Delegate TypeErasedEventHandler { get; internal set; }
        public RawEventDelegate RawHandler { get; }

        public ProcessProxyEventSubscriptionInfo(ProcessProxyImplementation owner, ReflectedEventInfo evt, RaiseEventDelegate raiseEventCallback)
        {
            m_owner = owner;
            Event = evt;
            RaiseEventCallback = raiseEventCallback;
            RawHandler = obj => RaiseEventCallback(m_owner, TypeErasedEventHandler, obj);
        }

        internal bool IsChange(Delegate handler, bool isAdd)
        {
            lock (this)
            {
                if (isAdd)
                {
                    bool wasNull = TypeErasedEventHandler is null;
                    TypeErasedEventHandler = Delegate.Combine(TypeErasedEventHandler, handler);
                    return wasNull;
                }
                else
                {
                    bool wasNotNull = TypeErasedEventHandler != null;
                    TypeErasedEventHandler = Delegate.Remove(TypeErasedEventHandler, handler);
                    return TypeErasedEventHandler is null && wasNotNull;
                }
            }
        }
    }
}
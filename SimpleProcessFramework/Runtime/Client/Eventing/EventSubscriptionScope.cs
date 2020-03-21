using Spfx.Reflection;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Spfx.Runtime.Client.Eventing
{
    internal class EventSubscriptionScope : Disposable
    {
        private readonly Dictionary<IClientInterprocessConnection, EventSubscriptionChangeRequest> m_changes = new Dictionary<IClientInterprocessConnection, EventSubscriptionChangeRequest>();
        private readonly TaskCompletionSource<VoidType> m_completion = new TaskCompletionSource<VoidType>(TaskCreationOptions.RunContinuationsAsynchronously);

        [ThreadStatic]
        private static EventSubscriptionScope t_current;

        internal static bool IsInScope => t_current != null;

        private EventSubscriptionScope()
        {
        }

        protected override void OnDispose()
        {
            Debug.Assert(ReferenceEquals(this, t_current));
            t_current = null;

            if (!m_completion.Task.IsCompleted)
            {
                SendEventChanges();
            }

            base.OnDispose();
        }

        private async void SendEventChanges()
        {
            try
            {
                var tasks = new List<Task>();
                foreach (var (conn, changes) in m_changes)
                {
                    tasks.Add(conn.ChangeEventSubscription(changes).AsTask());
                }

                await TaskEx.WhenAllOrRethrow(tasks).ConfigureAwait(false);
                m_completion.TryComplete();
            }
            catch (Exception ex)
            {
                m_completion.TrySetException(ex);
            }
        }

        private void ChangeEventSubscription(ProcessEndpointAddress endpoint, IClientInterprocessConnection conn, RawEventDelegate handler, ProcessProxyEventSubscriptionInfo eventInfo, bool isAdd)
        {
            if (!m_changes.TryGetValue(conn, out var changeRequest))
                m_changes[conn] = changeRequest = new EventSubscriptionChangeRequest();

            changeRequest.Changes.Add(new EventSubscriptionChange(endpoint, handler, eventInfo.Event.Name, isAdd));
        }


        internal static Task SubscribeEventsAsync(Action subscriptionCallback)
        {
            using var localState = Create();
            return localState.DoSubscribeEvents(subscriptionCallback);
        }

        private Task DoSubscribeEvents(Action subscriptionCallback)
        {
            try
            {
                subscriptionCallback();
            }
            catch (Exception ex)
            {
                m_completion.TrySetException(ex);
            }

            return m_completion.Task;
        }

        internal static void AddEventSubscription(ProcessEndpointAddress address, IClientInterprocessConnection conn, Delegate handler, ProcessProxyEventSubscriptionInfo eventInfo)
        {
            ChangeEvent(address, conn, handler, eventInfo, isAdd: true);
        }

        internal static void RemoveEventSubscription(ProcessEndpointAddress address, IClientInterprocessConnection conn, Delegate handler, ProcessProxyEventSubscriptionInfo eventInfo)
        {
            ChangeEvent(address, conn, handler, eventInfo, isAdd: false);
        }

        private static void ChangeEvent(ProcessEndpointAddress address, IClientInterprocessConnection conn, Delegate handler, ProcessProxyEventSubscriptionInfo eventInfo, bool isAdd)
        {
            if (!eventInfo.IsChange(handler, isAdd))
                return;

            using (GetTemporaryScope(isAdd))
            {
                GetCurrent().ChangeEventSubscription(address, conn, eventInfo.RawHandler, eventInfo, isAdd);
            }
        }

        private static IDisposable GetTemporaryScope(bool isAdd)
        {
            if (IsInScope)
                return null; // nothing to dispose if nested in an existing scope, the outer one will do the job.

            if (isAdd)
                ThrowMissingScope();

            return Create();
        }

        private static void ThrowMissingScope()
        {
            BadCodeAssert.ThrowInvalidOperation("Event Handlers may only be subscribed from within ProcessProxy.SubscribeEventsAsync");
        }

        private static EventSubscriptionScope GetCurrent()
        {
            var cur = t_current;
            if (cur is null)
                ThrowMissingScope();
            return cur;
        }

        private static EventSubscriptionScope Create()
        {
            if (IsInScope)
                BadCodeAssert.ThrowInvalidOperation("Calls to ProcessProxy.SubscribeEventsAsync cannot be nested");
            return t_current = new EventSubscriptionScope();
        }
    }
}
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Exceptions;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Runtime.Server;
using SimpleProcessFramework.Serialization;
using System;
using System.Collections.Generic;

namespace SimpleProcessFramework.Runtime.Server
{
    public interface IProcess
    {
        ProcessProxy ClusterProxy { get; }

        void HandleMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage);

        void RegisterEndpoint<T>(string address, T handler);

        ProcessEndpointAddress CreateRelativeAddress(string endpointAddress = null);
    }

    public class Process : IProcess
    {
        public const string MasterProcessUniqueId = "Master";

        public ProcessProxy ClusterProxy { get; }
        public string HostAuthority { get; }
        public string UniqueId { get; }
        public ProcessEndpointAddress UniqueAddress { get; }

        private readonly IBinarySerializer m_binarySerializer;
        private readonly Dictionary<string, IProcessEndpointHandler> m_endpointHandlers = new Dictionary<string, IProcessEndpointHandler>(ProcessEndpointAddress.StringComparer);

        public Process(string hostAuthority, string uniqueId, ITypeResolver typeResolver)
        {
            ClusterProxy = new ProcessProxy();
            HostAuthority = hostAuthority;
            UniqueAddress = CreateRelativeAddressInternal();
            m_binarySerializer = typeResolver.GetService<IBinarySerializer>();
        }

        public ProcessEndpointAddress CreateRelativeAddress(string endpointAddress = null)
        {
            if (string.IsNullOrWhiteSpace(endpointAddress))
                return UniqueAddress;
            return CreateRelativeAddressInternal(endpointAddress);
        }

        void IProcess.HandleMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage)
        {
            var message = wrappedMessage.Unwrap(m_binarySerializer);
            switch(message)
            {
                case IInterprocessRequest callReq:
                    InvokeEndpoint(source, callReq);
                    break;
            }
        }

        public void RegisterEndpoint<T>(string address, T handler)
        {
            var wrapper = ProcessEndpointHandlerFactory.Create(handler);

            lock (m_endpointHandlers)
            {
                m_endpointHandlers.Add(address, wrapper);
            }
        }

        private void InvokeEndpoint(IInterprocessClientProxy source, IInterprocessRequest callReq)
        {
            IProcessEndpointHandler ep;
            lock(m_endpointHandlers)
            {
                m_endpointHandlers.TryGetValue(callReq.Destination.FinalEndpoint, out ep);
            }

            if (ep is null)
                throw new EndpointNotFoundException(callReq.Destination);

            var requestContext = new InterprocessRequestContext(ep, source, callReq);
            ep.HandleMessage(requestContext);
        }

        private ProcessEndpointAddress CreateRelativeAddressInternal(string endpointAddress = null)
        {
            return new ProcessEndpointAddress(HostAuthority, UniqueId, endpointAddress);
        }
    }
}

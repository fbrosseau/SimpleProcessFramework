using SimpleProcessFramework.Interfaces;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Exceptions;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Runtime.Server;
using SimpleProcessFramework.Serialization;
using SimpleProcessFramework.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SimpleProcessFramework.Runtime.Server
{
    public interface IProcess
    {
        ProcessCreationInfo ProcessCreationInfo { get; }
        ProcessProxy ClusterProxy { get; }
        string HostAuthority { get; }
        string UniqueId { get; }
        ITypeResolver DefaultTypeResolver { get; }

        ProcessEndpointAddress UniqueAddress { get; }

        Task<bool> InitializeEndpointAsync(string uniqueId, Type endpointType, Type implementationType, bool failIfExists = true);
        Task<bool> InitializeEndpointAsync<T>(string address, T handler, bool failIfExists = true);
        Task<bool> InitializeEndpointAsync<T>(string address, bool failIfExists = true);
        Task<bool> InitializeEndpointAsync<T>(string address, Type impl, bool failIfExists = true);
        Task<bool> DestroyEndpoint(string uniqueId);

        ProcessEndpointAddress CreateRelativeAddress(string endpointAddress = null);
    }

    internal interface IProcessInternal : IProcess, IDisposable
    {
        Task InitializeAsync();

        void HandleMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage);
    }

    internal class Process2 : IProcessInternal
    {
        public const string MasterProcessUniqueId = "Master";

        public ProcessProxy ClusterProxy { get; }
        public string HostAuthority { get; }
        public string UniqueId { get; }
        public ProcessEndpointAddress UniqueAddress { get; }
        public ProcessCreationInfo ProcessCreationInfo { get; }
        public ITypeResolver DefaultTypeResolver { get; }

        private readonly IBinarySerializer m_binarySerializer;
        private readonly Dictionary<string, IProcessEndpointHandler> m_endpointHandlers = new Dictionary<string, IProcessEndpointHandler>(ProcessEndpointAddress.StringComparer);

        internal Process2(ProcessCluster root)
            : this("localhost", MasterProcessUniqueId, root.TypeResolver)
        {
            ProcessCreationInfo = new ProcessCreationInfo
            {
                ProcessUniqueId = MasterProcessUniqueId,
                ProcessName = Process.GetCurrentProcess().ProcessName,
                ProcessKind = ProcessUtilities.GetCurrentProcessKind()
            };

            InitializeAsync().ExpectAlreadyCompleted();
        }

        internal Process2(string hostAuthority, string uniqueId, ITypeResolver typeResolver)
        {
            ClusterProxy = new ProcessProxy();
            HostAuthority = hostAuthority;
            UniqueAddress = CreateRelativeAddressInternal();
            DefaultTypeResolver = typeResolver;
            m_binarySerializer = typeResolver.GetSingleton<IBinarySerializer>();
        }

        public void Dispose()
        {

        }

        public async Task InitializeAsync()
        {
            await InitializeEndpointAsync<IEndpointBroker>(WellKnownEndpoints.EndpointBroker);
        }

        public ProcessEndpointAddress CreateRelativeAddress(string endpointAddress = null)
        {
            if (string.IsNullOrWhiteSpace(endpointAddress))
                return UniqueAddress;
            return CreateRelativeAddressInternal(endpointAddress);
        }

        void IProcessInternal.HandleMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage)
        {
            var message = wrappedMessage.Unwrap(m_binarySerializer);
            switch(message)
            {
                case IInterprocessRequest callReq:
                    InvokeEndpoint(source, callReq);
                    break;
            }
        }

        public Task<bool> InitializeEndpointAsync<T>(string address, bool failIfExists = true)
        {
            return InitializeEndpointAsync(address, DefaultTypeResolver.CreateSingleton<T>());
        }

        public Task<bool> InitializeEndpointAsync<T>(string address, Type impl, bool failIfExists = true)
        {
            return InitializeEndpointAsync(address, typeof(T), impl, failIfExists);
        }

        public Task<bool> InitializeEndpointAsync<T>(string address, T handler, bool failIfExists = true)
        {
            return InitializeEndpointAsync(address, typeof(T), handler, failIfExists);
        }

        public Task<bool> InitializeEndpointAsync(string uniqueId, Type endpointType, Type implementationType, bool failIfExists)
        {
            var instance = DefaultTypeResolver.CreateInstance(endpointType, implementationType);
            try
            {
                return InitializeEndpointAsync(uniqueId, endpointType, instance, failIfExists);
            }
            catch
            {
                (instance as IDisposable).Dispose();
                throw;
            }
        }

        public async Task<bool> InitializeEndpointAsync(string address, Type interfaceType, object handler, bool failIfExists)
        {
            var wrapper = ProcessEndpointHandlerFactory.Create(handler, interfaceType);

            lock (m_endpointHandlers)
            {
                if (m_endpointHandlers.ContainsKey(address))
                {
                    if (failIfExists)
                        throw new EndpointAlreadyExistsException(address);
                    return false;
                }
                m_endpointHandlers.Add(address, wrapper);
            }

            try
            {
                await wrapper.InitializeAsync(this);
                return true;
            }
            catch
            {
                lock (m_endpointHandlers)
                {
                    m_endpointHandlers.Remove(address);
                }

                throw;
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

        public Task<bool> DestroyEndpoint(string uniqueId)
        {
            throw new NotImplementedException();
        }
    }
}

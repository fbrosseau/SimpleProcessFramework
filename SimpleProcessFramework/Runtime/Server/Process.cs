using SimpleProcessFramework.Utilities;
using SimpleProcessFramework.Interfaces;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Client;
using SimpleProcessFramework.Runtime.Exceptions;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Serialization;
using SimpleProcessFramework.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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

    internal interface IProcessInternal : IProcess, IAsyncDestroyable
    {
        Task InitializeAsync();

        void ProcessIncomingMessage(IInterprocessClientProxy source, IInterprocessMessage req);
    }

    internal class Process2 : AsyncDestroyable, IProcessInternal
    {
        public const string MasterProcessUniqueId = "Master";

        public ProcessProxy ClusterProxy { get; }

        public string HostAuthority { get; }
        public string UniqueId { get; }
        public ProcessEndpointAddress UniqueAddress { get; }
        public ProcessCreationInfo ProcessCreationInfo { get; }
        public ITypeResolver DefaultTypeResolver { get; }

        private readonly IBinarySerializer m_binarySerializer;
        private readonly IInternalMessageDispatcher m_outgoingMessageDispatcher;
        private readonly Dictionary<string, IProcessEndpointHandler> m_endpointHandlers = new Dictionary<string, IProcessEndpointHandler>(ProcessEndpointAddress.StringComparer);
        private IEndpointBroker m_endpointBroker;

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
            Guard.ArgumentNotNullOrEmpty(hostAuthority, nameof(hostAuthority));
            Guard.ArgumentNotNullOrEmpty(uniqueId, nameof(uniqueId));
            Guard.ArgumentNotNull(typeResolver, nameof(typeResolver));

            DefaultTypeResolver = typeResolver.CreateNewScope();

            UniqueId = uniqueId;
            HostAuthority = hostAuthority;
            UniqueAddress = CreateRelativeAddressInternal();
            m_binarySerializer = DefaultTypeResolver.GetSingleton<IBinarySerializer>();

            DefaultTypeResolver.RegisterSingleton<IInternalRequestsHandler>(new InternalRequestsHandler(hostAuthority));
            DefaultTypeResolver.RegisterSingleton<IProcess>(this);
            DefaultTypeResolver.RegisterSingleton<IProcessInternal>(this);
            DefaultTypeResolver.RegisterSingleton<IClientConnectionFactory>(new InternalClientConnectionFactory(DefaultTypeResolver));
            ClusterProxy = new ProcessProxy(DefaultTypeResolver);

            m_outgoingMessageDispatcher = DefaultTypeResolver.GetSingleton<IInternalMessageDispatcher>();
        }

        protected override void OnDispose()
        {
            List<IProcessEndpointHandler> endpoints;
            lock (m_endpointHandlers)
            {
                endpoints = m_endpointHandlers.Values.ToList();
                m_endpointHandlers.Clear();
            }

            foreach (var ep in endpoints)
            {
                ep.Dispose();
            }
        }

        protected async override Task OnTeardownAsync(CancellationToken ct)
        {
            List<IProcessEndpointHandler> endpoints;
            lock (m_endpointHandlers)
            {
                endpoints = m_endpointHandlers.Values.ToList();
                m_endpointHandlers.Clear();
            }

            var disposeTasks = new List<Task>();
            foreach (var ep in endpoints)
            {
                disposeTasks.Add(ep.TeardownAsync(ct));
            }

            await Task.WhenAll(disposeTasks);

            await base.OnTeardownAsync(ct);
        }

        public async Task InitializeAsync()
        {
            await InitializeEndpointAsync<IEndpointBroker>(WellKnownEndpoints.EndpointBroker).ConfigureAwait(false);
        }

        public ProcessEndpointAddress CreateRelativeAddress(string endpointAddress = null)
        {
            if (string.IsNullOrWhiteSpace(endpointAddress))
                return UniqueAddress;
            return CreateRelativeAddressInternal(endpointAddress);
        }

        void IProcessInternal.ProcessIncomingMessage(IInterprocessClientProxy source, IInterprocessMessage req)
        {
            if (req is WrappedInterprocessMessage wrappedMessage)
                req = wrappedMessage.Unwrap(m_binarySerializer);

            switch(req)
            {
                case IInterprocessRequest callReq:
                    InvokeEndpoint(source, callReq);
                    break;
                default:
                    throw new InvalidOperationException("Unhandled message!");
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
                ThrowIfDisposed();
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
                m_endpointHandlers.TryGetValue(callReq.Destination.LeafEndpoint, out ep);
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

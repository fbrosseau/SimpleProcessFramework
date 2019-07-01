using Spfx.Utilities;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Client;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Messages;
using Spfx.Serialization;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using Spfx.Diagnostics.Logging;

namespace Spfx.Runtime.Server
{
    public interface IProcess
    {
        ProcessCreationInfo ProcessCreationInfo { get; }
        ProcessProxy ClusterProxy { get; }
        string HostAuthority { get; }
        string UniqueId { get; }

        IProcessBroker ProcessBroker { get; }
        IEndpointBroker LocalEndpointBroker { get; }
        ITypeResolver DefaultTypeResolver { get; }
        WaitHandle TerminateEvent { get; }

        X509Certificate2 DefaultServerCertificate { get; }

        ProcessEndpointAddress UniqueAddress { get; }

        Task<ProcessCreationOutcome> InitializeEndpointAsync(string uniqueId, Type endpointType, Type implementationType, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists);
        Task<ProcessCreationOutcome> InitializeEndpointAsync<T>(string address, T handler, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists);
        Task<ProcessCreationOutcome> InitializeEndpointAsync<T>(string address, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists);
        Task<ProcessCreationOutcome> InitializeEndpointAsync<T>(string address, Type impl, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists);
        Task<bool> DestroyEndpoint(string uniqueId);

        Task AutoDestroyAsync();

        ProcessEndpointAddress CreateRelativeAddress(string endpointAddress = null);
    }

    internal interface IProcessInternal : IProcess, IIncomingClientMessagesHandler, IAsyncDestroyable
    {
        Task InitializeAsync();
        void ProcessIncomingMessage(IInterprocessClientProxy source, IInterprocessMessage req);
    }

    internal class Process2 : AsyncDestroyable, IProcessInternal
    {
        internal const string MasterProcessUniqueId = WellKnownEndpoints.MasterProcessUniqueId;

        public IProcessBroker ProcessBroker => GetLazyEndpoint($"/{MasterProcessUniqueId}/{WellKnownEndpoints.ProcessBroker}", ref m_processBroker);
        public IEndpointBroker LocalEndpointBroker => GetLazyEndpoint($"/{UniqueId}/{WellKnownEndpoints.EndpointBroker}", ref m_localEndpointBroker);

        public ProcessProxy ClusterProxy { get; }

        public string HostAuthority { get; }
        public string UniqueId { get; }
        public ProcessEndpointAddress UniqueAddress { get; }
        public ProcessCreationInfo ProcessCreationInfo { get; }
        public ITypeResolver DefaultTypeResolver { get; }
        public X509Certificate2 DefaultServerCertificate { get; }
        public WaitHandle TerminateEvent => m_terminateEvent;
        private readonly ManualResetEvent m_terminateEvent = new ManualResetEvent(false);

        private readonly IBinarySerializer m_binarySerializer;
        private readonly Dictionary<string, IProcessEndpointHandler> m_endpointHandlers = new Dictionary<string, IProcessEndpointHandler>(ProcessEndpointAddress.StringComparer);
        private readonly IClientConnectionManager m_connectionManager;
        private readonly ILogger m_logger;
        private IProcessBroker m_processBroker;
        private IEndpointBroker m_localEndpointBroker;

        internal Process2(ProcessCluster root)
            : this("localhost", MasterProcessUniqueId, root.TypeResolver)
        {
            ProcessCreationInfo = new ProcessCreationInfo
            {
                ProcessUniqueId = MasterProcessUniqueId,
                ProcessName = Process.GetCurrentProcess().ProcessName,
                ProcessKind = HostFeaturesHelper.LocalProcessKind
            };

            InitializeAsync().ExpectAlreadyCompleted();
        }

        internal Process2(string hostAuthority, string uniqueId, ITypeResolver typeResolver)
        {
            Guard.ArgumentNotNullOrEmpty(hostAuthority, nameof(hostAuthority));
            Guard.ArgumentNotNullOrEmpty(uniqueId, nameof(uniqueId));
            Guard.ArgumentNotNull(typeResolver, nameof(typeResolver));

            DefaultTypeResolver = typeResolver;

            UniqueId = uniqueId;
            HostAuthority = hostAuthority;
            UniqueAddress = CreateRelativeAddressInternal();
            m_binarySerializer = DefaultTypeResolver.GetSingleton<IBinarySerializer>();

            DefaultTypeResolver.RegisterSingleton<IInternalRequestsHandler>(new InternalRequestsHandler(hostAuthority));
            DefaultTypeResolver.RegisterSingleton<IProcess>(this);
            DefaultTypeResolver.RegisterSingleton<IProcessInternal>(this);
            DefaultTypeResolver.RegisterSingleton<ILocalConnectionFactory>(new InternalClientConnectionFactory(DefaultTypeResolver));

            m_logger = DefaultTypeResolver.GetLogger(GetType(), uniqueInstance: true);

            if (UniqueId != MasterProcessUniqueId) // otherwise ProcessBroker does it
                DefaultTypeResolver.RegisterSingleton<IIncomingClientMessagesHandler>(this);

            m_connectionManager = DefaultTypeResolver.CreateSingleton<IClientConnectionManager>();

            ClusterProxy = new ProcessProxy(DefaultTypeResolver);
        }

        protected override void OnDispose()
        {
            try
            {
                List<IProcessEndpointHandler> endpoints;
                lock (m_endpointHandlers)
                {
                    endpoints = m_endpointHandlers.Values.ToList();
                    m_endpointHandlers.Clear();
                }

                m_logger.Info?.Trace($"OnDispose of {endpoints.Count} endpoints");

                foreach (var ep in endpoints)
                {
                    ep.Dispose();
                }
            }
            finally
            {
                try
                {
                    m_terminateEvent.Set();
                }
                catch
                {
                }

                m_terminateEvent.Dispose();
            }

            base.OnDispose();
            m_logger.Dispose();
        }

        protected async override Task OnTeardownAsync(CancellationToken ct)
        {
            List<IProcessEndpointHandler> endpoints;
            lock (m_endpointHandlers)
            {
                endpoints = m_endpointHandlers.Values.ToList();
                m_endpointHandlers.Clear();
            }

            m_logger.Info?.Trace($"OnTeardownAsync of {endpoints.Count} endpoints");

            var disposeTasks = new List<Task>();
            foreach (var ep in endpoints)
            {
                disposeTasks.Add(ep.TeardownAsync(ct));
            }

            await Task.WhenAll(disposeTasks);

            m_logger.Info?.Trace($"OnTeardownAsync of endpoints completed");

            await base.OnTeardownAsync(ct);
        }

        public async Task InitializeAsync()
        {
            m_logger.Info?.Trace("InitializeAsync");
            await InitializeEndpointAsync<IEndpointBroker>(WellKnownEndpoints.EndpointBroker).ConfigureAwait(false);
            m_logger.Info?.Trace("InitializeAsync completed");
        }

        public ProcessEndpointAddress CreateRelativeAddress(string endpointAddress = null)
        {
            if (string.IsNullOrWhiteSpace(endpointAddress))
                return UniqueAddress;
            return CreateRelativeAddressInternal(endpointAddress);
        }

        void IIncomingClientMessagesHandler.ForwardMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage)
        {
            ((IProcessInternal)this).ProcessIncomingMessage(source, wrappedMessage);
        }
               
        void IProcessInternal.ProcessIncomingMessage(IInterprocessClientProxy source, IInterprocessMessage msg)
        {
            if (msg is WrappedInterprocessMessage wrappedMessage)
            {
                if (!wrappedMessage.IsRequest)
                {
                    var answerChannel = m_connectionManager.GetClientChannel(wrappedMessage.SourceConnectionId, mustExist: true);
                    answerChannel.SendMessage(wrappedMessage);
                    return;
                }

                msg = wrappedMessage.Unwrap(m_binarySerializer);
            }

            switch (msg)
            {
                case IInterprocessRequest callReq:
                    InvokeEndpoint(source, callReq);
                    break;
                default:
                    throw new InvalidOperationException("Unhandled message!");
            }
        }

        public Task<ProcessCreationOutcome> InitializeEndpointAsync<T>(string address, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists)
        {
            return InitializeEndpointAsync(address, DefaultTypeResolver.CreateSingleton<T>(), options);
        }

        public Task<ProcessCreationOutcome> InitializeEndpointAsync<T>(string address, Type impl, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists)
        {
            return InitializeEndpointAsync(address, typeof(T), impl, options);
        }

        public Task<ProcessCreationOutcome> InitializeEndpointAsync<T>(string address, T handler, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists)
        {
            return InitializeEndpointAsync(address, typeof(T), handler, options);
        }

        public Task<ProcessCreationOutcome> InitializeEndpointAsync(string uniqueId, Type endpointType, Type implementationType, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists)
        {
            var instance = DefaultTypeResolver.CreateInstance(endpointType, implementationType);
            try
            {
                return InitializeEndpointAsync(uniqueId, endpointType, instance, options);
            }
            catch
            {
                (instance as IDisposable)?.Dispose();
                throw;
            }
        }

        public async Task<ProcessCreationOutcome> InitializeEndpointAsync(string address, Type interfaceType, object handler, ProcessCreationOptions options)
        {
            var wrapper = ProcessEndpointHandlerFactory.Create(handler, interfaceType);
            bool removeFromEndpoints = false;
            try
            {
                m_logger.Info?.Trace($"InitializeEndpointAsync {address}");

                lock (m_endpointHandlers)
                {
                    ThrowIfDisposed();
                    if (m_endpointHandlers.ContainsKey(address))
                    {
                        m_logger.Info?.Trace($"InitializeEndpointAsync {address} already existed");

                        if ((options & ProcessCreationOptions.ThrowIfExists) != 0)
                            throw new EndpointAlreadyExistsException(address);
                        return ProcessCreationOutcome.AlreadyExists;
                    }
                    m_endpointHandlers.Add(address, wrapper);
                    removeFromEndpoints = true;
                }

                await wrapper.InitializeAsync(this);
                m_logger.Info?.Trace($"InitializeEndpointAsync succeeded");
                return ProcessCreationOutcome.CreatedNew;
            }
            catch
            {
                if (removeFromEndpoints)
                {
                    lock (m_endpointHandlers)
                    {
                        m_endpointHandlers.Remove(address);
                    }
                }

                wrapper.Dispose();
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

            var requestContext = new InterprocessRequestContext(DefaultTypeResolver, ep, source, callReq);
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

        public Task AutoDestroyAsync()
        {
            m_logger.Info?.Trace("AutoDestroyAsync");
            return TeardownAsync();
        }

        private T GetLazyEndpoint<T>(string addr, ref T cacheField)
        {
            if (cacheField != null)
                return cacheField;

            cacheField = ClusterProxy.CreateInterface<T>(addr);
            return cacheField;
        }
    }
}

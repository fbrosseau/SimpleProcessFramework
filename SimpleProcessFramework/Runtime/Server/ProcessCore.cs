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
    public readonly struct LocalProcessCreationResult
    {
        public ProcessCreationResults Result { get; }
        public object RawEndpointObject { get; }

        public LocalProcessCreationResult(ProcessCreationResults result, object endpoint)
        {
            Result = result;
            RawEndpointObject = endpoint;
        }
    }

    public interface IProcess
    {
        ProcessCreationInfo ProcessCreationInfo { get; }
        ProcessProxy ClusterProxy { get; }
        string HostAuthority { get; }
        string UniqueId { get; }

        IProcessBroker ProcessBroker { get; }
        IEndpointBroker LocalEndpointBroker { get; }
        ITypeResolver DefaultTypeResolver { get; }
        Task TerminateEvent { get; }

        X509Certificate2 DefaultServerCertificate { get; }

        ProcessEndpointAddress UniqueAddress { get; }

        ValueTask<LocalProcessCreationResult> InitializeEndpointAsync(string uniqueId, Type endpointType, Type implementationType, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists);
        ValueTask<LocalProcessCreationResult> InitializeEndpointAsync<T>(string address, T handler, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists);
        ValueTask<LocalProcessCreationResult> InitializeEndpointAsync<T>(string address, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists);
        ValueTask<LocalProcessCreationResult> InitializeEndpointAsync<T>(string address, Type impl, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists);
        ValueTask<bool> DestroyEndpoint(string uniqueId);

        ValueTask AutoDestroyAsync();

        ProcessEndpointAddress CreateRelativeAddress(string endpointAddress = null);
    }

    internal interface IProcessInternal : IProcess, IIncomingClientMessagesHandler, IAsyncDestroyable
    {
        Task InitializeAsync();
        void ProcessIncomingMessage(IInterprocessClientProxy source, IInterprocessMessage req);
    }

    internal class ProcessCore : AsyncDestroyable, IProcessInternal
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
        public Task TerminateEvent { get; }
        private readonly AsyncManualResetEvent m_terminateEvent = new AsyncManualResetEvent(false);
        private readonly IDisposable m_addressUriCacheRegistration;
        private readonly IBinarySerializer m_binarySerializer;
        private ProcessClusterConfiguration m_clusterConfig;
        private readonly Dictionary<string, IProcessEndpointHandler> m_endpointHandlers = new Dictionary<string, IProcessEndpointHandler>(ProcessEndpointAddress.StringComparer);
        private readonly IClientConnectionManager m_connectionManager;
        private readonly ILogger m_logger;
        private IProcessBroker m_processBroker;
        private IEndpointBroker m_localEndpointBroker;

        internal ProcessCore(ProcessCluster root)
            : this("localhost", MasterProcessUniqueId, root.TypeResolver)
        {
            ProcessCreationInfo = new ProcessCreationInfo
            {
                ProcessUniqueId = MasterProcessUniqueId,
                ProcessName = Process.GetCurrentProcess().ProcessName,
                TargetFramework = TargetFramework.CurrentFrameworkWithoutRuntime
            };

            InitializeAsync().ExpectAlreadyCompleted();
        }

        internal ProcessCore(string hostAuthority, string uniqueId, ITypeResolver typeResolver)
        {
            Guard.ArgumentNotNullOrEmpty(hostAuthority, nameof(hostAuthority));
            Guard.ArgumentNotNullOrEmpty(uniqueId, nameof(uniqueId));
            Guard.ArgumentNotNull(typeResolver, nameof(typeResolver));

            DefaultTypeResolver = typeResolver;

            UniqueId = uniqueId;
            HostAuthority = hostAuthority;
            UniqueAddress = CreateRelativeAddressInternal();

            m_addressUriCacheRegistration = ProcessEndpointAddressCache.RegisterWellKnownAddress(UniqueAddress);

            m_binarySerializer = DefaultTypeResolver.CreateSingleton<IBinarySerializer>();

            DefaultTypeResolver.RegisterSingleton<IInternalRequestsHandler>(new InternalRequestsHandler(hostAuthority));
            DefaultTypeResolver.RegisterSingleton<IProcess>(this);
            DefaultTypeResolver.RegisterSingleton<IProcessInternal>(this);
            DefaultTypeResolver.RegisterSingleton<ILocalConnectionFactory>(new InternalClientConnectionFactory(DefaultTypeResolver));

            m_logger = DefaultTypeResolver.GetLogger(GetType(), uniqueInstance: true);

            if (UniqueId != MasterProcessUniqueId) // otherwise ProcessBroker does it
                DefaultTypeResolver.RegisterSingleton<IIncomingClientMessagesHandler>(this);

            m_connectionManager = DefaultTypeResolver.CreateSingleton<IClientConnectionManager>();

            ClusterProxy = new ProcessProxy(DefaultTypeResolver);
            TerminateEvent = m_terminateEvent.WaitAsync().AsTask();
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

                m_addressUriCacheRegistration?.Dispose();

                base.OnDispose();
                m_logger.Dispose();
            }
        }

        protected override async ValueTask OnTeardownAsync(CancellationToken ct)
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

            m_logger.Info?.Trace("OnTeardownAsync of endpoints completed");

            await base.OnTeardownAsync(ct);
        }

        public async Task InitializeAsync()
        {
            m_logger.Info?.Trace("InitializeAsync");
            m_clusterConfig = DefaultTypeResolver.CreateSingleton<ProcessClusterConfiguration>();
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
                    answerChannel.SendMessageToClient(wrappedMessage);
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

        public ValueTask<LocalProcessCreationResult> InitializeEndpointAsync<T>(string address, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists)
        {
            return InitializeEndpointAsync(address, DefaultTypeResolver.CreateSingleton<T>(), options);
        }

        public ValueTask<LocalProcessCreationResult> InitializeEndpointAsync<T>(string address, Type impl, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists)
        {
            return InitializeEndpointAsync(address, typeof(T), impl, options);
        }

        public ValueTask<LocalProcessCreationResult> InitializeEndpointAsync<T>(string address, T handler, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists)
        {
            return InitializeEndpointAsync(address, typeof(T), handler, options);
        }

        public ValueTask<LocalProcessCreationResult> InitializeEndpointAsync(string uniqueId, Type endpointType, Type implementationType, ProcessCreationOptions options = ProcessCreationOptions.ThrowIfExists)
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

        public async ValueTask<LocalProcessCreationResult> InitializeEndpointAsync(string endpointId, Type interfaceType, object handler, ProcessCreationOptions options)
        {
            Guard.ArgumentNotNull(handler, nameof(handler));

            ReflectedAssemblyInfo.AddWellKnownAssembly(interfaceType.Assembly);
            ReflectedAssemblyInfo.AddWellKnownAssembly(handler.GetType().Assembly);

            var wrapper = ProcessEndpointHandlerFactory.Create(this, endpointId, handler, interfaceType);
            bool removeFromEndpoints = false;
            try
            {
                m_logger.Info?.Trace($"InitializeEndpointAsync {endpointId}");

                lock (m_endpointHandlers)
                {
                    ThrowIfDisposed();
                    if (m_endpointHandlers.TryGetValue(endpointId, out var alreadyExisting))
                    {
                        m_logger.Info?.Trace($"InitializeEndpointAsync {endpointId} already existed");

                        if ((options & ProcessCreationOptions.ThrowIfExists) != 0)
                            throw new EndpointAlreadyExistsException(endpointId);
                        return new LocalProcessCreationResult(ProcessCreationResults.AlreadyExists, alreadyExisting.ImplementationObject);
                    }
                    m_endpointHandlers.Add(endpointId, wrapper);
                    removeFromEndpoints = true;
                }

                await wrapper.InitializeAsync();
                m_logger.Info?.Trace("InitializeEndpointAsync succeeded");
                return new LocalProcessCreationResult(ProcessCreationResults.CreatedNew, handler);
            }
            catch
            {
                if (removeFromEndpoints)
                {
                    lock (m_endpointHandlers)
                    {
                        m_endpointHandlers.Remove(endpointId);
                    }
                }

                wrapper.Dispose();
                throw;
            }
        }

        private void InvokeEndpoint(IInterprocessClientProxy source, IInterprocessRequest callReq)
        {
            var ep = GetEndpoint(callReq.Destination);
            var requestContext = new InterprocessRequestContext(DefaultTypeResolver, ep, source, callReq);
            ep.HandleMessage(requestContext);
        }

        private IProcessEndpointHandler GetEndpoint(ProcessEndpointAddress addr)
        {
            var epId = addr.EndpointId;
            if (string.IsNullOrEmpty(epId))
                throw new EndpointNotFoundException(addr);

            IProcessEndpointHandler ep;
            lock (m_endpointHandlers)
            {
                m_endpointHandlers.TryGetValue(epId, out ep);
            }

            if (ep is null)
                throw new EndpointNotFoundException(addr);
            return ep;
        }

        private ProcessEndpointAddress CreateRelativeAddressInternal(string endpointAddress = null)
        {
            return ProcessEndpointAddress.Create(HostAuthority, UniqueId, endpointAddress);
        }

        public async ValueTask<bool> DestroyEndpoint(string uniqueId)
        {
            IProcessEndpointHandler ep;

            lock (m_endpointHandlers)
            {
                m_endpointHandlers.TryGetValue(uniqueId, out ep);
            }

            if (ep is null)
                return false;

            using (ep)
            {
                try
                {
                    await ep.TeardownAsync(m_clusterConfig.DestroyEndpointTimeout);
                }
                finally
                {
                    lock (m_endpointHandlers)
                    {
                        m_endpointHandlers.Remove(uniqueId);
                    }
                }

                return true;
            }
        }

        public ValueTask AutoDestroyAsync()
        {
            m_logger.Info?.Trace("AutoDestroyAsync");
            return new ValueTask(TeardownAsync());
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Utilities;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Messages;
using Spfx.Runtime.Server.Processes;
using Spfx.Utilities.Threading;
using Spfx.Diagnostics.Logging;

namespace Spfx.Runtime.Server
{
    public interface IInternalProcessBroker : IProcessBroker, IAsyncDestroyable
    {
        IProcess MasterProcess { get; }
        void ForwardMessageToProcess(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage);
    }

    internal class ProcessBroker : AbstractProcessEndpoint, IInternalProcessBroker, IInternalMessageDispatcher
    {
        private readonly Dictionary<string, IProcessHandle> m_subprocesses = new Dictionary<string, IProcessHandle>(ProcessEndpointAddress.StringComparer);
        private readonly ProcessCluster m_owner;
        private readonly ITypeResolver m_typeResolver;
        private readonly ProcessClusterConfiguration m_config;
        private readonly ILogger m_logger;

        public IProcess MasterProcess => m_masterProcess;

        public string LocalProcessUniqueId => WellKnownEndpoints.MasterProcessUniqueId;

        private readonly IProcessInternal m_masterProcess;

        public event EventHandler<ProcessEventArgs> ProcessCreated;
        public event EventHandler<ProcessEventArgs> ProcessLost;

        public ProcessBroker(ProcessCluster owner)
        {
            m_owner = owner;
            m_typeResolver = owner.TypeResolver;
            m_typeResolver.RegisterSingleton<IInternalMessageDispatcher>(this);
            m_config = m_typeResolver.GetSingleton<ProcessClusterConfiguration>();
            m_logger = m_typeResolver.GetLogger(GetType(), uniqueInstance: true);

            m_masterProcess = new ProcessCore(owner);
            m_typeResolver.RegisterSingleton<IProcess>(m_masterProcess);
        }

        protected override void OnDispose()
        {
            m_logger.Info?.Trace(nameof(OnDispose));

            List<IProcessHandle> processes;
            lock (m_subprocesses)
            {
                processes = m_subprocesses.Values.ToList();
                m_subprocesses.Clear();
            }

            foreach (var p in processes)
            {
                p.Dispose();
            }

            m_masterProcess.Dispose();

            base.OnDispose();
        }

        protected override async ValueTask OnTeardownAsync(CancellationToken ct)
        {
            m_logger.Info?.Trace(nameof(OnTeardownAsync));

            List<IProcessHandle> processes;
            lock (m_subprocesses)
            {
                processes = m_subprocesses.Values.ToList();
            }

            var teardownTasks = new List<Task>();

            m_logger.Info?.Trace($"Starting teardown of {processes.Count} processes");

            foreach (var p in processes)
            {
                teardownTasks.Add(p.TeardownAsync(ct).AsTask());
            }

            await Task.WhenAll(teardownTasks);

            m_logger.Info?.Trace("Teardown of processes completed");

            await m_masterProcess.TeardownAsync(ct);

            m_logger.Info?.Trace("Teardown of master process completed");

            await base.OnTeardownAsync(ct);
        }

        private IProcessHandle GetSubprocess(string targetProcess, bool throwIfMissing)
        {
            IProcessHandle target;
            lock (m_subprocesses)
            {
                m_subprocesses.TryGetValue(targetProcess, out target);
            }

            if (target != null || !throwIfMissing)
                return target;

            throw new ProcessNotFoundException(targetProcess);
        }

        public async Task<ProcessCreationOutcome> CreateProcess(ProcessCreationRequest req)
        {
            var info = req.ProcessInfo;
            m_logger.Info?.Trace($"CreateProcess {info.ProcessUniqueId}");

            if (ProcessEndpointAddress.StringComparer.Equals(info.ProcessUniqueId, ProcessCore.MasterProcessUniqueId))
                throw new InvalidOperationException("The master process cannot be created this way");

            req.EnsureIsValid();

            var handle = CreateNewProcessHandle(info);

            try
            {
                IProcessHandle existingHandle;
                lock (m_subprocesses)
                {
                    ThrowIfDisposing();

                    if (m_subprocesses.TryGetValue(info.ProcessUniqueId, out existingHandle))
                    {
                        handle.Dispose();

                        if ((req.Options & ProcessCreationOptions.ThrowIfExists) != 0)
                            throw new ProcessAlreadyExistsException(info.ProcessUniqueId);
                    }
                    else
                    {
                        m_subprocesses.Add(info.ProcessUniqueId, handle);
                    }
                }

                if (existingHandle != null)
                {
                    m_logger.Info?.Trace($"CreateProcess {info.ProcessUniqueId}: Already exists");
                    await existingHandle.WaitForInitializationComplete();
                    m_logger.Debug?.Trace($"CreateProcess {info.ProcessUniqueId}: Process init complete");
                    return ProcessCreationOutcome.AlreadyExists;
                }

                m_logger.Debug?.Trace($"CreateProcess {info.ProcessUniqueId}: Starting creation");
                await handle.CreateProcess();
                m_logger.Debug?.Trace($"CreateProcess {info.ProcessUniqueId}: Creation complete. PID is {handle.ProcessInfo.OsPid}");

                return ProcessCreationOutcome.CreatedNew;
            }
            catch(Exception ex)
            {
                m_logger.Warn?.Trace(ex, $"CreateProcess {info.ProcessUniqueId} failed: " + ex.Message);
                lock (m_subprocesses)
                {
                    if (m_subprocesses.TryGetValue(info.ProcessUniqueId, out var knownHandle) && ReferenceEquals(knownHandle, handle))
                    {
                        m_subprocesses.Remove(info.ProcessUniqueId);
                    }
                }

                m_logger.Debug?.Trace($"CreateProcess teardown failed process {info.ProcessUniqueId}");
                await handle.TeardownAsync(TimeSpan.FromSeconds(5));
                m_logger.Debug?.Trace($"CreateProcess teardown complete of failed process {info.ProcessUniqueId}");
                throw;
            }
        }

        public async Task<ProcessAndEndpointCreationOutcome> CreateProcessAndEndpoint(ProcessCreationRequest processReq, EndpointCreationRequest endpointReq)
        {
            m_logger.Info?.Trace($"CreateProcessAndEndpoint {processReq.ProcessInfo.ProcessUniqueId}/{endpointReq.EndpointId}");

            processReq.EnsureIsValid();
            endpointReq.EnsureIsValid();

            var processOutcome = ProcessCreationOutcome.Failure;
            ProcessCreationOutcome endpointOutcome;

            try
            {
                processOutcome = await CreateProcess(processReq);
                m_logger.Info?.Trace($"CreateProcessAndEndpoint {processReq.ProcessInfo.ProcessUniqueId}/{endpointReq.EndpointId} -> CreateProcess result is {processOutcome}");
                var addr = $"/{processReq.ProcessInfo.ProcessUniqueId}/{WellKnownEndpoints.EndpointBroker}";
                var processBroker = MasterProcess.ClusterProxy.CreateInterface<IEndpointBroker>(addr);
                endpointOutcome = await processBroker.CreateEndpoint(endpointReq);
                m_logger.Info?.Trace($"CreateProcessAndEndpoint {processReq.ProcessInfo.ProcessUniqueId}/{endpointReq.EndpointId} -> CreateEndpoint result is {endpointOutcome}");
            }
            catch(Exception ex)
            {
                m_logger.Warn?.Trace(ex, $"CreateProcessAndEndpoint {processReq.ProcessInfo.ProcessUniqueId}/{endpointReq.EndpointId} failed: " + ex.Message);

                if (processOutcome == ProcessCreationOutcome.CreatedNew)
                {
                    await DestroyProcess(processReq.ProcessInfo.ProcessUniqueId, onlyIfEmpty: true);
                }

                throw;
            }

            return new ProcessAndEndpointCreationOutcome(processOutcome, endpointOutcome);
        }

        private IProcessHandle CreateNewProcessHandle(ProcessCreationInfo info)
        {
            info.TargetFramework = info.TargetFramework.GetBestAvailableFramework(m_config);

            switch (info.TargetFramework.ProcessKind)
            {
                case ProcessKind.AppDomain:
                    return new AppDomainProcessHandle(info, m_typeResolver);
                case ProcessKind.DirectlyInRootProcess:
                    return new SameProcessFakeHandle(info, m_typeResolver);
                case ProcessKind.Netfx:
                case ProcessKind.Netfx32:
                case ProcessKind.Netcore:
                case ProcessKind.Netcore32:
                case ProcessKind.Default:
                case ProcessKind.Wsl:
                    return GenericRemoteTargetHandle.Create(m_config, info, m_typeResolver);
                default:
                    throw new PlatformNotSupportedException($"ProcessKind {info.TargetFramework} is not supported");
            }
        }

        public async Task<bool> DestroyProcess(string processUniqueId, bool onlyIfEmpty = true)
        {
            m_logger.Info?.Trace($"DestroyProcess {processUniqueId}");

            if (ProcessEndpointAddress.StringComparer.Equals(processUniqueId, ProcessCore.MasterProcessUniqueId))
                throw new InvalidOperationException("The master process cannot be destroyed this way");

            using (var target = GetSubprocess(processUniqueId, throwIfMissing: false))
            {
                if (target is null)
                    return false;

                try
                {
                    await target.TeardownAsync().ConfigureAwait(false);
                    m_logger.Debug?.Trace($"DestroyProcess completed for {processUniqueId}");
                }
                catch (Exception ex)
                {
                    m_logger.Warn?.Trace(ex, $"DestroyProcess failed for {processUniqueId}: " + ex.Message);
                }
            }

            return true;
        }

        void IInternalMessageDispatcher.ForwardOutgoingMessage(IInterprocessClientChannel source, IInterprocessMessage req, CancellationToken ct)
        {
            Guard.ArgumentNotNull(source, nameof(source));
            Guard.ArgumentNotNull(req, nameof(req));

            if (ProcessEndpointAddress.StringComparer.Equals(req.Destination.TargetProcess, ProcessCore.MasterProcessUniqueId))
            {
                var sourceProxy = source.GetWrapperProxy();
                m_masterProcess.ProcessIncomingMessage(sourceProxy, req);
            }
            else
            {
                var target = GetSubprocess(req.Destination.TargetProcess, throwIfMissing: true);
                target.HandleMessage(source.UniqueId, req);
            }
        }

        public void ForwardMessageToProcess(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage)
        {
            Guard.ArgumentNotNull(source, nameof(source));
            Guard.ArgumentNotNull(wrappedMessage, nameof(wrappedMessage));

            string targetProcess;
            if (wrappedMessage.IsRequest)
            {
                targetProcess = wrappedMessage.Destination.TargetProcess;
            }
            else
            {
                targetProcess = wrappedMessage.SourceConnectionId.Substring(0, wrappedMessage.SourceConnectionId.IndexOf('/'));
            }

            if (ProcessEndpointAddress.StringComparer.Equals(targetProcess, ProcessCore.MasterProcessUniqueId))
            {
                m_masterProcess.ProcessIncomingMessage(source, wrappedMessage);
            }
            else
            {
                IProcessHandle target = GetSubprocess(targetProcess, throwIfMissing: true);
                target.HandleMessage(source.UniqueId, wrappedMessage);
            }
        }

        Task<ProcessClusterHostInformation> IProcessBroker.GetHostInformation()
        {
            return Task.FromResult(ProcessClusterHostInformation.GetCurrent());
        }

        public Task<List<ProcessInformation>> GetAllProcesses()
        {
            lock (m_subprocesses)
            {
                return Task.FromResult(m_subprocesses.Values.Select(p => p.ProcessInfo).ToList());
            }
        }

        public Task<ProcessInformation> GetProcessInformation(string processName)
        {
            return Task.FromResult(GetSubprocess(processName, throwIfMissing: true).ProcessInfo);
        }
    }
}
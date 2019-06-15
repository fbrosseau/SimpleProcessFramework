using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace Spfx.Runtime.Server
{
    public interface IInternalProcessBroker : IProcessBroker, IIncomingClientMessagesHandler, IAsyncDestroyable
    {
        IProcess MasterProcess { get; }
    }

    public interface IInternalMessageDispatcher
    {
        string LocalProcessUniqueId { get; }
        void ForwardOutgoingMessage(IInterprocessClientChannel source, IInterprocessMessage req, CancellationToken ct);
    }

    internal class ProcessBroker : AbstractProcessEndpoint, IInternalProcessBroker, IInternalMessageDispatcher
    {
        private readonly Dictionary<string, IProcessHandle> m_subprocesses = new Dictionary<string, IProcessHandle>(ProcessEndpointAddress.StringComparer);
        private readonly ProcessCluster m_owner;
        private readonly ITypeResolver m_typeResolver;
        private readonly ProcessClusterConfiguration m_config;

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

            m_masterProcess = new Process2(owner);
            m_typeResolver.RegisterSingleton<IProcess>(m_masterProcess);
        }

        protected override void OnDispose()
        {
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

        protected async override Task OnTeardownAsync(CancellationToken ct)
        {
            List<IProcessHandle> processes;
            lock (m_subprocesses)
            {
                processes = m_subprocesses.Values.ToList();
            }

            var teardownTasks = new List<Task>();

            foreach (var p in processes)
            {
                teardownTasks.Add(p.TeardownAsync(ct));
            }

            await Task.WhenAll(teardownTasks);

            await m_masterProcess.TeardownAsync(ct);

            await base.OnTeardownAsync(ct);
        }

        void IIncomingClientMessagesHandler.ForwardMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage)
        {
            string targetProcess;
            if (wrappedMessage.IsRequest)
            {
                targetProcess = wrappedMessage.Destination.TargetProcess;
            }
            else
            {
                targetProcess = wrappedMessage.SourceConnectionId.Substring(0, wrappedMessage.SourceConnectionId.IndexOf('/'));
            }

            if (ProcessEndpointAddress.StringComparer.Equals(targetProcess, Process2.MasterProcessUniqueId))
            {
                m_masterProcess.ProcessIncomingMessage(source, wrappedMessage);
            }
            else
            {
                IProcessHandle target = GetSubprocess(targetProcess, throwIfMissing: true);
                target.HandleMessage(source, wrappedMessage);
            }
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

            if (ProcessEndpointAddress.StringComparer.Equals(info.ProcessUniqueId, Process2.MasterProcessUniqueId))
                throw new InvalidOperationException("The master process cannot be created this way");

            ApplyConfigToProcessRequest(req);

            var handle = CreateNewProcessHandle(info);

            try
            {
                lock (m_subprocesses)
                {
                    ThrowIfDisposing();

                    if (m_subprocesses.ContainsKey(info.ProcessUniqueId))
                    {
                        if (req.MustCreateNew)
                            throw new InvalidOperationException("The process already exists");
                        return ProcessCreationOutcome.ProcessAlreadyExists;
                    }

                    m_subprocesses.Add(info.ProcessUniqueId, handle);
                }

                var punchPayload = new ProcessSpawnPunchPayload
                {
                    HostAuthority = m_owner.MasterProcess.HostAuthority,
                    ProcessKind = info.ProcessKind,
                    ProcessUniqueId = info.ProcessUniqueId,
                    ParentProcessId = Process.GetCurrentProcess().Id
                };

                await handle.CreateProcess(punchPayload);

                return ProcessCreationOutcome.CreatedNew;
            }
            catch
            {
                lock (m_subprocesses)
                {
                    if (m_subprocesses.TryGetValue(info.ProcessUniqueId, out var knownHandle) && ReferenceEquals(knownHandle, handle))
                    {
                        m_subprocesses.Remove(info.ProcessUniqueId);
                    }
                }

                await handle.TeardownAsync(TimeSpan.FromSeconds(5));
                throw;
            }
        }

        public async Task<ProcessCreationOutcome> CreateEndpoint(ProcessCreationRequest processReq, EndpointCreationRequest endpointReq)
        {
            processReq.EnsureIsValid();
            endpointReq.EnsureIsValid();

            var processOutcome = ProcessCreationOutcome.ProcessAlreadyExists;

            try
            {
                processOutcome = await CreateProcess(processReq);
                var addr = $"/{processReq.ProcessInfo.ProcessUniqueId}/{WellKnownEndpoints.EndpointBroker}";
                var processBroker = MasterProcess.ClusterProxy.CreateInterface<IEndpointBroker>(addr);
                return await processBroker.CreateEndpoint(endpointReq);
            }
            catch
            {
                if(processOutcome == ProcessCreationOutcome.CreatedNew)
                {
                    await DestroyProcess(processReq.ProcessInfo.ProcessUniqueId, onlyIfEmpty: true);
                }

                throw;
            }
        }

        private void ApplyConfigToProcessRequest(ProcessCreationRequest req)
        {
            if (req.ProcessInfo.ProcessKind == ProcessKind.Default)
                req.ProcessInfo.ProcessKind = m_config.DefaultProcessKind;

            req.ProcessInfo.EnsureIsValid();
        }

        private IProcessHandle CreateNewProcessHandle(ProcessCreationInfo info)
        {
            void CheckWindows(string error)
            {
                if (HostFeaturesHelper.LocalMachineOsKind != OsKind.Windows)
                    throw new PlatformNotSupportedException(error);
            }
            void Check32()
            {
                CheckWindows("Running 32-bit is only supported on Windows");
                if (!m_config.Enable32Bit)
                    throw new PlatformNotSupportedException("This current configuration does not support 32-bit processes");
            }

            void CheckNetfx()
            {
                CheckWindows("Running .Net Framework (Netfx) is only supported on Windows");
                if (!m_config.EnableNetfx)
                    throw new PlatformNotSupportedException("This current configuration does not support .Net Framework");
            }

            void CheckNetcore()
            {
                if (!m_config.EnableNetcore)
                    throw new PlatformNotSupportedException("This current configuration does not support .Net core");
            }

            switch (info.ProcessKind)
            {
                case ProcessKind.AppDomain:
                    CheckWindows("This platform does not support AppDomains");
                    if (!HostFeaturesHelper.LocalProcessKind.IsNetfx())
                        throw new PlatformNotSupportedException("Only .Net Framework (Netfx) hosts can create AppDomain processes");
                    if (!m_config.EnableAppDomains)
                        throw new PlatformNotSupportedException("The current configuration does not support AppDomains");
                    CheckNetfx();
                    return new AppDomainProcessHandle(info, m_typeResolver);
                case ProcessKind.DirectlyInRootProcess:
                    if (!m_config.EnableFakeProcesses)
                        throw new PlatformNotSupportedException("This current configuration does not support " + nameof(ProcessKind.DirectlyInRootProcess));
                    return new SameProcessFakeHandle(info, m_typeResolver);
                case ProcessKind.Netfx:
                    CheckNetfx();
                    break;
                case ProcessKind.Netfx32:
                    CheckNetfx(); Check32();
                    break;
                case ProcessKind.Netcore:
                    CheckNetcore();
                    break;
                case ProcessKind.Netcore32:
                    CheckNetcore(); Check32();
                    break;
                case ProcessKind.Wsl:
                    CheckWindows("This platform does not support WSL");
                    if (!m_config.EnableWsl)
                        throw new PlatformNotSupportedException("This current configuration does not support WSL");
                    break;
                case ProcessKind.Default:
                    break;
            }

            if (!HostFeaturesHelper.IsProcessKindSupported(info.ProcessKind))
                throw new PlatformNotSupportedException("This platform does not support ProcessKind " + info.ProcessKind);

            if (HostFeaturesHelper.LocalMachineOsKind == OsKind.Windows && !m_config.UseGenericProcessSpawnOnWindows)
                return new GenericChildProcessHandle(info, m_typeResolver);
            return new GenericChildProcessHandle(info, m_typeResolver);
        }

        public async Task<bool> DestroyProcess(string processUniqueId, bool onlyIfEmpty = true)
        {
            if (ProcessEndpointAddress.StringComparer.Equals(processUniqueId, Process2.MasterProcessUniqueId))
                throw new InvalidOperationException("The master process cannot be destroyed this way");

            var target = GetSubprocess(processUniqueId, throwIfMissing: false);
            if (target is null)
                return false;

            try
            {
                await target.TeardownAsync().ConfigureAwait(false);
            }
            catch
            {
            }

            return true;
        }

        void IInternalMessageDispatcher.ForwardOutgoingMessage(IInterprocessClientChannel source, IInterprocessMessage req, CancellationToken ct)
        {
            Guard.ArgumentNotNull(source, nameof(source));
            Guard.ArgumentNotNull(req, nameof(req));

            var sourceProxy = source.GetWrapperProxy();
            if (ProcessEndpointAddress.StringComparer.Equals(req.Destination.TargetProcess, Process2.MasterProcessUniqueId))
            {
                m_masterProcess.ProcessIncomingMessage(sourceProxy, req);
            }
            else
            {
                var target = GetSubprocess(req.Destination.TargetProcess, throwIfMissing: true);
                target.ProcessIncomingRequest(sourceProxy, req);
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
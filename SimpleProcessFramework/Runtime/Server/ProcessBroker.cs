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
using Spfx.Runtime.Server.Processes.Windows;

namespace Spfx.Runtime.Server
{
    public interface IInternalProcessBroker : IProcessBroker, IAsyncDestroyable
    {
        IProcess MasterProcess { get; }
        void ForwardMessageToProcess(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage);

        void AddProcessLostHandler(string processId, Action<EndpointLostEventArgs> onProcessLost);
        void RemoveProcessLostHandler(string processId, Action<EndpointLostEventArgs> onProcessLost);
    }

    internal class ProcessBroker : AbstractProcessEndpoint, IInternalProcessBroker, IInternalMessageDispatcher
    {
        private class ProcessHandleInfo
        {
            public IProcessHandle Handle;
            public HashSet<Action<EndpointLostEventArgs>> EndpointLostListeners = new HashSet<Action<EndpointLostEventArgs>>();
        }

        private readonly Dictionary<string, ProcessHandleInfo> m_subprocesses = new Dictionary<string, ProcessHandleInfo>(ProcessEndpointAddress.StringComparer);
        private readonly ITypeResolver m_typeResolver;
        private readonly ProcessClusterConfiguration m_config;
        private readonly ILogger m_logger;

        public IProcess MasterProcess => m_masterProcess;

        public string LocalProcessUniqueId => WellKnownEndpoints.MasterProcessUniqueId;

        private readonly IProcessInternal m_masterProcess;

        public event EventHandler<ProcessEventArgs> ProcessCreated { add { } remove { } }
        public event EventHandler<ProcessEventArgs> ProcessLost { add { } remove { } }

        public ProcessBroker(ProcessCluster owner)
        {
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

            List<ProcessHandleInfo> processes;
            lock (m_subprocesses)
            {
                processes = m_subprocesses.Values.ToList();
                m_subprocesses.Clear();
            }

            foreach (var p in processes)
            {
                p.Handle.Dispose();
            }

            m_masterProcess.Dispose();

            base.OnDispose();
        }

        protected override async ValueTask OnTeardownAsync(CancellationToken ct)
        {
            m_logger.Info?.Trace(nameof(OnTeardownAsync));

            List<ProcessHandleInfo> processes;
            lock (m_subprocesses)
            {
                processes = m_subprocesses.Values.ToList();
            }

            m_logger.Info?.Trace($"Starting teardown of {processes.Count} processes");
            await TeardownAll(processes.Select(h => h.Handle), ct).ConfigureAwait(false);
            m_logger.Info?.Trace("Teardown of processes completed");

            await m_masterProcess.TeardownAsync(ct).ConfigureAwait(false);

            m_logger.Info?.Trace("Teardown of master process completed");

            await base.OnTeardownAsync(ct).ConfigureAwait(false);
        }

        private IProcessHandle GetSubprocess(string targetProcess, bool throwIfMissing)
        {
            ProcessHandleInfo target;
            lock (m_subprocesses)
            {
                m_subprocesses.TryGetValue(targetProcess, out target);
            }

            if (target != null || !throwIfMissing)
                return target?.Handle;

            throw new ProcessNotFoundException(targetProcess);
        }

        public async Task<ProcessCreationResults> CreateProcess(ProcessCreationRequest req)
        {
            var info = req.ProcessInfo;
            m_logger.Info?.Trace($"CreateProcess {info.ProcessUniqueId}");

            if (ProcessEndpointAddress.StringComparer.Equals(info.ProcessUniqueId, ProcessCore.MasterProcessUniqueId))
                BadCodeAssert.ThrowInvalidOperation("The master process cannot be created this way");

            req.EnsureIsValid();

            var handle = CreateNewProcessHandle(info);

            try
            {
                ProcessHandleInfo existingHandle;
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
                        m_subprocesses.Add(info.ProcessUniqueId, new ProcessHandleInfo { Handle = handle });
                    }
                }

                if (existingHandle != null)
                {
                    m_logger.Info?.Trace($"CreateProcess {info.ProcessUniqueId}: Already exists");
                    await existingHandle.Handle.WaitForInitializationComplete().ConfigureAwait(false);
                    m_logger.Debug?.Trace($"CreateProcess {info.ProcessUniqueId}: Process init complete");
                    return ProcessCreationResults.AlreadyExists;
                }

                handle.ProcessExited += OnProcessExited;

                m_logger.Debug?.Trace($"CreateProcess {info.ProcessUniqueId}: Starting creation");
                await handle.CreateProcess().ConfigureAwait(false);
                m_logger.Debug?.Trace($"CreateProcess {info.ProcessUniqueId}: Creation complete. PID is {handle.ProcessInfo.OsPid}");

                return ProcessCreationResults.CreatedNew;
            }
            catch (Exception ex)
            {
                m_logger.Warn?.Trace(ex, $"CreateProcess {info.ProcessUniqueId} failed: " + ex.Message);
                await DestroyHandleAsync(info.ProcessUniqueId, handle).ConfigureAwait(false);
                m_logger.Debug?.Trace($"CreateProcess teardown complete of failed process {info.ProcessUniqueId}");
                throw;
            }
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            var proc = (IProcessHandle)sender;

            m_logger.Info?.Trace("OnProcessExited: " + proc.ProcessUniqueId);

            DestroyProcess(proc.ProcessUniqueId).FireAndForget();
        }

        public async Task<ProcessAndEndpointCreationOutcome> CreateProcessAndEndpoint(ProcessCreationRequest processReq, EndpointCreationRequest endpointReq)
        {
            m_logger.Info?.Trace($"CreateProcessAndEndpoint {processReq.ProcessInfo.ProcessUniqueId}/{endpointReq.EndpointId}");

            processReq.EnsureIsValid();
            endpointReq.EnsureIsValid();

            var processOutcome = ProcessCreationResults.Failure;
            ProcessCreationResults endpointOutcome;

            try
            {
                processOutcome = await CreateProcess(processReq).ConfigureAwait(false);
                m_logger.Info?.Trace($"CreateProcessAndEndpoint {processReq.ProcessInfo.ProcessUniqueId}/{endpointReq.EndpointId} -> CreateProcess result is {processOutcome}");
                var addr = $"/{processReq.ProcessInfo.ProcessUniqueId}/{WellKnownEndpoints.EndpointBroker}";
                var processBroker = MasterProcess.ClusterProxy.CreateInterface<IEndpointBroker>(addr);
                endpointOutcome = await processBroker.CreateEndpoint(endpointReq).ConfigureAwait(false);
                m_logger.Info?.Trace($"CreateProcessAndEndpoint {processReq.ProcessInfo.ProcessUniqueId}/{endpointReq.EndpointId} -> CreateEndpoint result is {endpointOutcome}");
            }
            catch(Exception ex)
            {
                m_logger.Warn?.Trace(ex, $"CreateProcessAndEndpoint {processReq.ProcessInfo.ProcessUniqueId}/{endpointReq.EndpointId} failed: " + ex.Message);

                if (processOutcome == ProcessCreationResults.CreatedNew)
                {
                    await DestroyProcess(processReq.ProcessInfo.ProcessUniqueId, onlyIfEmpty: true).ConfigureAwait(false);
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
                BadCodeAssert.ThrowInvalidOperation("The master process cannot be destroyed this way");

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
                finally
                {
                    await DestroyHandleAsync(processUniqueId).ConfigureAwait(false);
                }
            }

            return true;
        }

        void IInternalMessageDispatcher.ForwardOutgoingMessage(IInterprocessClientChannel source, IInterprocessMessage req)
        {
            Guard.ArgumentNotNull(source, nameof(source));
            Guard.ArgumentNotNull(req, nameof(req));

            if (ProcessEndpointAddress.StringComparer.Equals(req.Destination.ProcessId, ProcessCore.MasterProcessUniqueId))
            {
                var sourceProxy = source.GetWrapperProxy();
                m_masterProcess.ProcessIncomingMessage(sourceProxy, req);
            }
            else
            {
                var target = GetSubprocess(req.Destination.ProcessId, throwIfMissing: true);
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
                targetProcess = wrappedMessage.Destination.ProcessId;
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
                Exception failureBackToSource;
                try
                {
                    IProcessHandle target = GetSubprocess(targetProcess, throwIfMissing: false);
                    if (target is null)
                    {
                        m_logger.Debug?.Trace($"Received request for non-existant process {targetProcess}: {wrappedMessage.GetTinySummaryString()}");
                        failureBackToSource = new ProcessNotFoundException(targetProcess);
                    }
                    else
                    {
                        target.HandleMessage(source.UniqueId, wrappedMessage);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    failureBackToSource = ex;
                    m_logger.Warn?.Trace(failureBackToSource, $"Failed to process request to process {targetProcess} from {source}: ({wrappedMessage.GetTinySummaryString()}");
                }

                if (wrappedMessage.IsRequest && StatefulInterprocessMessageExtensions.IsValidCallId(wrappedMessage.CallId))
                {
                    var resp = m_typeResolver.CreateSingleton<IFailureCallResponsesFactory>().Create(wrappedMessage.CallId, failureBackToSource);
                    source.SendMessage(resp);
                }
            }
        }

        void IInternalProcessBroker.AddProcessLostHandler(string processId, Action<EndpointLostEventArgs> onProcessLost)
        {
            lock (m_subprocesses)
            {
                if (m_subprocesses.TryGetValue(processId, out var info))
                    info.EndpointLostListeners.Add(onProcessLost);
            }
        }

        void IInternalProcessBroker.RemoveProcessLostHandler(string processId, Action<EndpointLostEventArgs> onProcessLost)
        {
            lock (m_subprocesses)
            {
                if (m_subprocesses.TryGetValue(processId, out var info))
                    info.EndpointLostListeners.Remove(onProcessLost);
            }
        }

        private async Task DestroyHandleAsync(string processUniqueId, IProcessHandle expectedHandle = null)
        {
            m_logger.Debug?.Trace("DestroyHandle: " + processUniqueId);

            ProcessHandleInfo knownProcess;
            bool raiseProcessLost = false;
            lock (m_subprocesses)
            {
                if (m_subprocesses.TryGetValue(processUniqueId, out knownProcess))
                {
                    if (expectedHandle != null && knownProcess.Handle != expectedHandle)
                    {
                        m_logger.Debug?.Trace("DestroyHandle: Unexpected handle, not disposing " + processUniqueId);
                    }
                    else
                    {
                        m_subprocesses.Remove(processUniqueId);
                        raiseProcessLost = true;
                    }
                }
                else
                {
                    m_logger.Debug?.Trace("DestroyHandle: Handle unknown for " + processUniqueId);
                    return;
                }
            }

            knownProcess.Handle.ProcessExited -= OnProcessExited;

            if (raiseProcessLost && knownProcess.EndpointLostListeners?.Count > 0)
            {
                m_logger.Debug?.Trace("DestroyHandle: Raising EndpointLost for " + processUniqueId);

                var e = new EndpointLostEventArgs(ProcessEndpointAddress.RelativeClusterAddress.Combine(processUniqueId), EndpointLostReason.Destroyed);
                foreach (var handler in knownProcess.EndpointLostListeners)
                {
                    handler(e);
                }
            }

            await knownProcess.Handle.TeardownAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            m_logger.Debug?.Trace("DestroyHandle: Completed for " + processUniqueId);
        }

        Task<ProcessClusterHostInformation> IProcessBroker.GetHostInformation()
        {
            return Task.FromResult(ProcessClusterHostInformation.GetCurrent());
        }

        public Task<List<ProcessInformation>> GetAllProcesses()
        {
            lock (m_subprocesses)
            {
                return Task.FromResult(m_subprocesses.Values.Select(p => p.Handle.ProcessInfo).ToList());
            }
        }

        public Task<ProcessInformation> GetProcessInformation(string processName)
        {
            return Task.FromResult(GetSubprocess(processName, throwIfMissing: true).ProcessInfo);
        }
    }
}
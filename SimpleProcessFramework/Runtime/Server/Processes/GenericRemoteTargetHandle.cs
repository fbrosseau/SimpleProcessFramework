using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Messages;
using Spfx.Runtime.Server.Processes.Ipc;
using Spfx.Utilities;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server.Processes
{
    internal abstract class GenericRemoteTargetHandle : GenericProcessHandle, IIpcConnectorListener
    {
        protected static string EscapeArg(string a) => ProcessUtilities.FormatArgument(a);

        private Process m_targetProcess;

        public string ProcessName => ProcessCreationInfo.ProcessName;
        public ProcessSpawnPunchPayload RemotePunchPayload { get; private set; }

        private readonly TaskCompletionSource<string> m_processExitEvent = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        private MasterProcessIpcConnector m_ipcConnector;

        protected GenericRemoteTargetHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
            : base(info, typeResolver)
        {
        }

        internal static GenericRemoteTargetHandle Create(ProcessClusterConfiguration config, ProcessCreationInfo info, ITypeResolver typeResolver)
        {
            if (HostFeaturesHelper.IsWindows && !config.UseGenericProcessSpawnOnWindows)
                return new DotNetProcessTargetHandle(info, typeResolver);
            return new DotNetProcessTargetHandle(info, typeResolver);
        }

        protected override async Task<ProcessInformation> CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload)
        {
            RemotePunchPayload = punchPayload;

            using (var disposeBag = new DisposeBag())
            using (var cts = new CancellationTokenSource(Config.CreateProcessTimeout))
            {
                var ct = cts.Token;
                var remoteProcessHandles = CreatePunchHandles();
                disposeBag.Add(remoteProcessHandles);

                try
                {
                    m_targetProcess = await SpawnProcess(remoteProcessHandles, ct).WithCancellation(ct);
                }
                catch (Exception ex)
                {
                    OnProcessLost("SpawnProcess failed: " + ex.Message);
                    throw await GetInitFailureException(ex);
                }

                var connector = new MasterProcessIpcConnector(this, remoteProcessHandles, TypeResolver);
                disposeBag.Add(connector);

                var initTask = connector.InitializeAsync(ct).WithCancellation(ct);
                var failureTask = m_processExitEvent.Task;
                var winnerTask = await Task.WhenAny(initTask, failureTask);
                if (ReferenceEquals(winnerTask, failureTask) || initTask.IsFaultedOrCanceled())
                {
                    var ex = initTask.IsFaultedOrCanceled() ? initTask.GetExceptionOrCancel() : null;
                    throw await GetInitFailureException(ex);
                }

                m_ipcConnector = connector;
                disposeBag.ReleaseAll();

                OnInitializationCompleted();

                return new ProcessInformation(ProcessUniqueId, m_targetProcess.Id, ProcessCreationInfo.TargetFramework);
            }
        }

        protected virtual void OnInitializationCompleted()
        {
        }

        protected virtual Task<Exception> GetInitFailureException(Exception caughtException)
        {
            caughtException?.Rethrow();
            throw new ProcessInitializationException();
        }

        protected virtual IRemoteProcessInitializer CreatePunchHandles()
        {
            if (HostFeaturesHelper.IsWindows)
            {
                if (ProcessCreationInfo.TargetFramework.ProcessKind == ProcessKind.Wsl)
                    return new WslProcessSpawnPunchHandles();
                return new WindowsProcessSpawnPunchHandles();
            }

            throw new NotImplementedException();
        }

        protected abstract Task<Process> SpawnProcess(IRemoteProcessInitializer punchHandles, CancellationToken ct);
        
        protected void OnProcessLost(string reason, Exception ex = null)
        {
            m_processExitEvent.TrySetResult(reason);
        }

        protected override async Task OnTeardownAsync(CancellationToken ct)
        {
            if (m_ipcConnector != null)
                await m_ipcConnector.TeardownAsync(ct).ConfigureAwait(false);
        }

        protected override void OnDispose()
        {
            m_ipcConnector?.Dispose();
            base.OnDispose();
        }

        void IIpcConnectorListener.OnTeardownRequestReceived()
        {
            throw new NotImplementedException();
        }

        protected override void TransferMessageToRemote(string sourceConnectionId, WrappedInterprocessMessage wrappedMessage)
        {
            wrappedMessage.SourceConnectionId = sourceConnectionId;
            m_ipcConnector.ForwardMessage(wrappedMessage);
        }

        Task IIpcConnectorListener.CompleteInitialization()
        {
            return Task.CompletedTask;
        }

        void IIpcConnectorListener.OnMessageReceived(WrappedInterprocessMessage msg)
        {
            OnMessageReceivedFromProcess(msg);
        }

        void IIpcConnectorListener.OnRemoteEndLost(string msg, Exception ex)
        {
            OnProcessLost(msg, ex);
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Interfaces;
using Spfx.Reflection;
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
                    var spawnTask = SpawnProcess(remoteProcessHandles, ct).WithCancellation(ct);
                    m_targetProcess = spawnTask.Result;
                }
                catch (Exception ex)
                {
                    OnProcessLost("SpawnProcess failed: " + ex.Message);
                    throw;
                }

                var connector = new MasterProcessIpcConnector(this, remoteProcessHandles, TypeResolver);
                disposeBag.Add(connector);

                var initTask = connector.InitializeAsync(ct).WithCancellation(ct);
                await TaskEx.ExpectFirstTask(initTask, m_processExitEvent.Task);

                m_ipcConnector = connector;
                disposeBag.ReleaseAll();

                return new ProcessInformation(ProcessUniqueId, m_targetProcess.Id, ProcessCreationInfo.ProcessKind);
            }
        }

        protected virtual IProcessSpawnPunchHandles CreatePunchHandles()
        {
            return HostFeaturesHelper.IsWindows
                ? new WindowsProcessSpawnPunchHandles()
                : throw new NotImplementedException();
        }

        protected abstract Task<Process> SpawnProcess(IProcessSpawnPunchHandles punchHandles, CancellationToken ct);
        
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

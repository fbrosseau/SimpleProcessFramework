using Spfx.Interfaces;
using Spfx.Io;
using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Messages;
using Spfx.Runtime.Server.Processes.Ipc;
using Spfx.Runtime.Server.Processes.Windows;
using Spfx.Subprocess;
using Spfx.Utilities;
using Spfx.Utilities.Runtime;
using Spfx.Utilities.Threading;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes
{
    internal abstract class GenericRemoteTargetHandle : GenericProcessHandle, IIpcConnectorListener
    {
        private Process m_targetProcess;

        public string ProcessName => ProcessCreationInfo.ProcessName;
        public ProcessSpawnPunchPayload RemotePunchPayload { get; private set; }

        private MasterProcessIpcConnector m_ipcConnector;

        protected GenericRemoteTargetHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
            : base(info, typeResolver)
        {
        }

        internal static GenericRemoteTargetHandle Create(ProcessClusterConfiguration config, ProcessCreationInfo info, ITypeResolver typeResolver)
        {
            if (HostFeaturesHelper.IsWindows && !config.UseGenericProcessSpawnOnWindows)
                return new WindowsProcessTargetHandle(info, typeResolver);
            return new ManagedProcessTargetHandle(info, typeResolver);
        }

        protected override async Task<ProcessInformation> CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload, CancellationToken ct)
        {
            RemotePunchPayload = punchPayload;

            using var disposeBag = new DisposeBag();
            using var remoteProcessHandles = CreatePunchHandles();

            try
            {
                await Task.Run(async () =>
                {
                    await remoteProcessHandles.InitializeAsync(RemotePunchPayload, ct).ConfigureAwait(false);
                    m_targetProcess = await SpawnProcess(remoteProcessHandles, ct).ConfigureAwait(false);
                    await remoteProcessHandles.CompleteHandshakeAsync().ConfigureAwait(false);
                }, ct).WithCancellation(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ReportFatalException(ex);
                ex = await GetInitFailureException().ConfigureAwait(false);
                OnProcessLost("SpawnProcess failed", ex);
                throw;
            }

            var streams = remoteProcessHandles.AcquireIOStreams();
            var writer = PipeWriterFactory.CreateWriter(streams.writeStream, ProcessUniqueId + " - MasterWrite");
            disposeBag.Add(writer);
            var reader = PipeWriterFactory.CreateReader(streams.readStream, ProcessUniqueId + " - MasterRead");
            disposeBag.Add(reader);

            m_ipcConnector = new MasterProcessIpcConnector(this, reader, writer, TypeResolver);
            disposeBag.Add(m_ipcConnector);

            var initTask = m_ipcConnector.InitializeAsync(ct).WithCancellation(ct);
            var failureTask = ProcessExitEvent;
            var winnerTask = await Task.WhenAny(initTask, failureTask).ConfigureAwait(false);
            if (ReferenceEquals(winnerTask, failureTask) || initTask.IsFaultedOrCanceled())
            {
                var ex = initTask.IsFaultedOrCanceled() ? initTask.GetExceptionOrCancel() : null;
                ReportFatalException(ex);
                (await GetInitFailureException().ConfigureAwait(false)).Rethrow();
            }

            disposeBag.ReleaseAll();

            OnInitializationCompleted();

            return new ProcessInformation(ProcessUniqueId, m_targetProcess.Id, ProcessCreationInfo.TargetFramework);
        }

        protected virtual void OnInitializationCompleted()
        {
        }

        protected virtual IRemoteProcessInitializer CreatePunchHandles()
        {
            if (HostFeaturesHelper.IsWindows)
            {
                if (ProcessCreationInfo.TargetFramework.ProcessKind == ProcessKind.Wsl)
                    return new WslProcessSpawnPunchHandles();
                return new WindowsPunchHandlesThroughStdIn();
            }

            throw new NotImplementedException();
        }

        protected abstract Task<Process> SpawnProcess(IRemoteProcessInitializer punchHandles, CancellationToken ct);

        protected void HandleProcessExit(Process process)
        {
            var exitCode = process.SafeGetExitCode(defaultValue: (int)SubprocessExitCodes.Unknown);
            OnProcessLost(exitCode);
        }

        protected void OnProcessLost(int exitCodeNumber)
        {
            var exitCode = (SubprocessExitCodes)exitCodeNumber;
            if (!Enum.IsDefined(typeof(SubprocessExitCodes), exitCode))
                exitCode = SubprocessExitCodes.Unknown;

            var msg = $"The process has exited with code {exitCode} ({exitCodeNumber})";
            OnProcessLost(msg, new SubprocessLostException(msg) { ExitCode = exitCode, ExitCodeNumber = exitCodeNumber });
        }

        protected override async ValueTask OnTeardownAsync(CancellationToken ct)
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

        Task IIpcConnectorListener.CompleteInitialization(CancellationToken ct)
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

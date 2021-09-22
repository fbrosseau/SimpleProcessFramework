using System;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Diagnostics.Logging;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Messages;
using Spfx.Runtime.Server.Processes.Hosting;
using Spfx.Serialization;
using Spfx.Utilities;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server.Processes
{
    internal abstract class GenericProcessHandle : AsyncDestroyable, IProcessHandle, IMessageCallbackChannel
    {
        public TargetFramework TargetFramework => ProcessCreationInfo.TargetFramework;
        public string ProcessUniqueId => ProcessCreationInfo.ProcessUniqueId;
        public ProcessCreationInfo ProcessCreationInfo { get; }
        public ProcessInformation ProcessInfo { get; private set; }
        public event EventHandler ProcessExited;

        protected ITypeResolver TypeResolver { get; }
        protected ProcessClusterConfiguration Config { get; }
        protected IIncomingClientMessagesHandler ProcessBroker { get; }
        protected ILogger Logger { get; }
        protected IBinarySerializer BinarySerializer { get; }

        protected Task<string> ProcessExitEvent => m_processExitEvent.Task;
        private readonly TaskCompletionSource<string> m_processExitEvent = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly AsyncManualResetEvent m_initEvent = new AsyncManualResetEvent();
        private Exception m_firstCaughtException;
        private CancellationTokenSource m_createProcessCancellation;

        protected GenericProcessHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
        {
            ProcessCreationInfo = info;
            TypeResolver = typeResolver;
            Config = typeResolver.GetSingleton<ProcessClusterConfiguration>();
            BinarySerializer = typeResolver.CreateSingleton<IBinarySerializer>();
            ProcessBroker = typeResolver.GetSingleton<IIncomingClientMessagesHandler>();
            Logger = typeResolver.GetLogger(GetType(), uniqueInstance: true, friendlyName: info.ProcessUniqueId);
        }

        public async Task CreateProcess()
        {
            var masterProcess = TypeResolver.GetSingleton<IInternalProcessBroker>().MasterProcess;

            var punchPayload = new ProcessSpawnPunchPayload
            {
                HostAuthority = masterProcess.HostAuthority,
                ProcessKind = ProcessCreationInfo.TargetFramework.ProcessKind,
                ProcessUniqueId = ProcessCreationInfo.ProcessUniqueId,
                ParentProcessId = ProcessUtilities.CurrentProcessId,
                TypeResolverFactory = Config.TypeResolverFactoryType?.AssemblyQualifiedName,
                HandshakeTimeout = (int)Config.CreateProcessTimeout.TotalMilliseconds
            };

            try
            {
                using var cts = new CancellationTokenSource(Config.CreateProcessTimeout);
                m_createProcessCancellation = cts;

                Logger.Debug?.Trace("Starting CreateProcess...");
                ProcessInfo = await CreateActualProcessAsync(punchPayload, cts.Token).ConfigureAwait(false);
                m_initEvent.Set();
                m_createProcessCancellation = null;
                Logger.Info?.Trace($"CreateProcess succeeded (PID {ProcessInfo.OsPid})");
            }
            catch (Exception ex)
            {
                ReportFatalException(ex);
                ex = await GetInitFailureException().ConfigureAwait(false);

                Logger.Warn?.Trace(ex, "CreateProcess failed: " + ex.Message);
                m_initEvent.Dispose();
                ex.Rethrow();
            }
        }

        protected override void OnDispose()
        {
            OnProcessLost("Dispose");

            Logger.Info?.Trace("OnDispose");
            m_initEvent.Dispose();
            base.OnDispose();
            Logger.Dispose();
        }

        protected void OnProcessLost(string reason, Exception ex = null)
        {
            ex = ex is SubprocessLostException ? ex : new SubprocessLostException(reason, ex);
            ReportFatalException(ex);

            Logger.Info?.Trace(ex, "The process was lost: " + reason);

            if (m_processExitEvent.TrySetResult(reason))
            {
                ProcessExited?.Invoke(this, EventArgs.Empty);
            }
        }

        protected abstract Task<ProcessInformation> CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload, CancellationToken ct);

        void IProcessHandle.HandleMessage(string connectionId, WrappedInterprocessMessage wrappedMessage)
        {
            PrepareTransferToRemote(connectionId, wrappedMessage).FireAndForget();
        }

        public void HandleMessage(string connectionId, IInterprocessMessage msg)
        {
            var wrapped = WrappedInterprocessMessage.Wrap(msg, BinarySerializer);
            wrapped.SourceConnectionId = connectionId;
            PrepareTransferToRemote(connectionId, wrapped).FireAndForget();
        }

        private async ValueTask PrepareTransferToRemote(string connectionId, WrappedInterprocessMessage wrappedMessage)
        {
            try
            {
                await WaitForInitializationComplete().ConfigureAwait(false);
                TransferMessageToRemote(connectionId, wrappedMessage);
            }
            catch(Exception ex)
            {
                OnDeliveryFailed(wrappedMessage, ex);
            }
        }

        protected abstract void TransferMessageToRemote(string sourceConnectionId, WrappedInterprocessMessage wrappedMessage);

        protected void OnMessageReceivedFromProcess(WrappedInterprocessMessage msg)
        {
            DispatchMessageBackToMaster(msg);
        }

        protected void OnDeliveryFailed(WrappedInterprocessMessage wrappedMessage, Exception ex)
        {
            Logger.Warn?.Trace(ex, $"OnDeliveryFailed: {ex.Message}");
            throw new NotImplementedException();
        }

        private void DispatchMessageBackToMaster(WrappedInterprocessMessage msg)
        {
            ProcessBroker.ForwardMessage(new ShallowConnectionProxy(this, msg.SourceConnectionId), msg);
        }

        ValueTask<IInterprocessClientChannel> IMessageCallbackChannel.GetClientInfo(string uniqueId)
        {
            throw new NotImplementedException();
        }

        public ValueTask WaitForInitializationComplete()
        {
            return m_initEvent.WaitAsync();
        }

        protected virtual Task<Exception> GetInitFailureException()
        {
            return Task.FromResult(m_firstCaughtException ?? new ProcessInitializationException());
        }

        protected bool ReportFatalException(Exception ex)
        {
            m_createProcessCancellation?.SafeCancelAndDisposeAsync();
            m_createProcessCancellation = null;
            return null == Interlocked.CompareExchange(ref m_firstCaughtException, ex, null);
        }

        public override string ToString()
        {
            return $"{ProcessUniqueId}->{GetType().Name}";
        }
    }
}
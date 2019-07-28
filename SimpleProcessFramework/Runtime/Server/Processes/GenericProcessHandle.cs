﻿using System;
using System.Threading.Tasks;
using Spfx.Diagnostics.Logging;
using Spfx.Interfaces;
using Spfx.Reflection;
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
        protected ITypeResolver TypeResolver { get; }
        protected ProcessClusterConfiguration Config { get; }
        public ProcessInformation ProcessInfo { get; private set; }
        private readonly IIncomingClientMessagesHandler m_processBroker;
        protected ILogger Logger { get; }

        private readonly AsyncManualResetEvent m_initEvent = new AsyncManualResetEvent();

        protected IBinarySerializer BinarySerializer { get; }

        protected GenericProcessHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
        {
            ProcessCreationInfo = info;
            TypeResolver = typeResolver;
            Config = typeResolver.GetSingleton<ProcessClusterConfiguration>();
            BinarySerializer = typeResolver.GetSingleton<IBinarySerializer>();
            m_processBroker = typeResolver.GetSingleton<IIncomingClientMessagesHandler>();
            Logger = typeResolver.GetLogger(GetType(), uniqueInstance: true);
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
                TypeResolverFactory = Config.TypeResolverFactoryType?.AssemblyQualifiedName
            };

            try
            {
                Logger.Debug?.Trace("Starting CreateProcess...");
                ProcessInfo = await CreateActualProcessAsync(punchPayload);
                m_initEvent.Set();
                Logger.Info?.Trace($"CreateProcess succeeded (PID {ProcessInfo.OsPid})");
            }
            catch (Exception ex)
            {
                Logger.Warn?.Trace(ex, "CreateProcess failed: " + ex.Message);
                m_initEvent.Dispose(ex);
                throw;
            }
        }

        protected override void OnDispose()
        {
            Logger.Info?.Trace("OnDispose");
            m_initEvent.Dispose();
            base.OnDispose();
            Logger.Dispose();
        }

        protected abstract Task<ProcessInformation> CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload);

        void IProcessHandle.HandleMessage(string connectionId, WrappedInterprocessMessage wrappedMessage)
        {
            _ = PrepareTransferToRemote(connectionId, wrappedMessage);
        }

        public void HandleMessage(string connectionId, IInterprocessMessage msg)
        {
            var wrapped = WrappedInterprocessMessage.Wrap(msg, BinarySerializer);
            wrapped.SourceConnectionId = connectionId;
            _ = PrepareTransferToRemote(connectionId, wrapped);
        }

        private async ValueTask PrepareTransferToRemote(string connectionId, WrappedInterprocessMessage wrappedMessage)
        {
            try
            {
                await WaitForInitializationComplete();
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
            m_processBroker.ForwardMessage(new ShallowConnectionProxy(this, msg.SourceConnectionId), msg);
        }

        Task<IInterprocessClientChannel> IMessageCallbackChannel.GetClientInfo(string uniqueId)
        {
            throw new NotImplementedException();
        }

        public ValueTask WaitForInitializationComplete()
        {
            return m_initEvent.WaitAsync();
        }
    }
}
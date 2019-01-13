﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using SimpleProcessFramework.Interfaces;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Exceptions;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Runtime.Server.Processes;

namespace SimpleProcessFramework.Runtime.Server
{
    public interface IInternalProcessBroker : IProcessBroker
    {
        IProcess MasterProcess { get; }
        void ForwardMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage);
    }

    internal class ProcessBroker : AbstractProcessEndpoint, IInternalProcessBroker, IProcessBroker
    {
        private readonly Dictionary<string, IProcessHandle> m_subprocesses = new Dictionary<string, IProcessHandle>(ProcessEndpointAddress.StringComparer);
        private readonly ProcessCluster m_owner;
        private readonly ITypeResolver m_typeResolver;
        private readonly ProcessClusterConfiguration m_config;

        public IProcess MasterProcess => m_masterProcess;
        private IProcessInternal m_masterProcess;

        public ProcessBroker(ProcessCluster owner)
        {
            m_owner = owner;
            m_typeResolver = owner.TypeResolver;
            m_config = m_typeResolver.GetSingleton<ProcessClusterConfiguration>();

            m_masterProcess = new Process2(owner);
            m_typeResolver.RegisterSingleton<IProcess>(m_masterProcess);
        }

        void IInternalProcessBroker.ForwardMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage)
        {
            if (ProcessEndpointAddress.StringComparer.Equals(wrappedMessage.Destination.TargetProcess, Process2.MasterProcessUniqueId))
            {
                m_masterProcess.HandleMessage(source, wrappedMessage);
            }
            else
            {
                IProcessHandle target = GetSubprocess(wrappedMessage.Destination.TargetProcess, throwIfMissing: true);
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

        public async Task<bool> CreateProcess(ProcessCreationInfo info, bool mustCreate)
        {
            if (ProcessEndpointAddress.StringComparer.Equals(info.ProcessUniqueId, Process2.MasterProcessUniqueId))
                throw new InvalidOperationException("The master process cannot be created this way");

            info.EnsureIsValid();

            var handle = CreateNewProcessHandle(info);

            try
            {
                lock (m_subprocesses)
                {
                    if (m_subprocesses.ContainsKey(info.ProcessUniqueId))
                    {
                        if (mustCreate)
                            throw new InvalidOperationException("The process already exists");
                        return false;
                    }

                    m_subprocesses.Add(info.ProcessUniqueId, handle);
                }

                var punchPayload = new ProcessSpawnPunchPayload
                {
                    HostAuthority = m_owner.MasterProcess.HostAuthority,
                    ProcessKind = info.ProcessKind.ToString(),
                    ProcessUniqueId = info.ProcessUniqueId,
                    ParentProcessId = Process.GetCurrentProcess().Id
                };

                await handle.CreateActualProcessAsync(punchPayload);

                return true;
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

                handle.Dispose();
                throw;
            }
        }

        private IProcessHandle CreateNewProcessHandle(ProcessCreationInfo info)
        {
            switch (info.ProcessKind)
            {
                case ProcessKind.Netfx:
                    if (!m_config.SupportNetfx)
                        throw new PlatformNotSupportedException("This platform does not support .Net Framework");
                    break;
                case ProcessKind.Netfx32:
                    if (!m_config.SupportNetfx)
                        throw new PlatformNotSupportedException("This platform does not support .Net Framework");
                    if (!m_config.Support32Bit)
                        throw new PlatformNotSupportedException("This platform does not support 32-bit processes");
                    break;
                case ProcessKind.Netcore:
                    if (!m_config.SupportNetcore)
                        throw new PlatformNotSupportedException("This platform does not support .Net core");
                    break;
                case ProcessKind.Netcore32:
                    if (!m_config.SupportNetcore)
                        throw new PlatformNotSupportedException("This platform does not support .Net core");
                    if (!m_config.Support32Bit)
                        throw new PlatformNotSupportedException("This platform does not support 32-bit processes");
                    break;
                case ProcessKind.Default:
                    break;
                default:
                    throw new PlatformNotSupportedException("This platform does not support ProcessKind " + info.ProcessKind);
            }

#if WINDOWS_BUILD
            if (!m_config.UseGenericProcessSpawnOnWindows)
                return new GenericChildProcessHandle(info, m_typeResolver);
#endif
            return new GenericChildProcessHandle(info, m_typeResolver);
        }

        public async Task<bool> DestroyProcess(string processUniqueId)
        {
            if (ProcessEndpointAddress.StringComparer.Equals(processUniqueId, Process2.MasterProcessUniqueId))
                throw new InvalidOperationException("The master process cannot be destroyed this way");

            var target = GetSubprocess(processUniqueId, throwIfMissing: false);
            if (target is null)
                return false;

            try
            {
                await target.DestroyAsync();
            }
            catch
            {
            }

            return true;
        }
    }
}
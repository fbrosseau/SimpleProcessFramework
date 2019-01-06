using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SimpleProcessFramework.Interfaces;
using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Exceptions;
using SimpleProcessFramework.Runtime.Messages;

namespace SimpleProcessFramework.Runtime.Server
{
    public interface IInternalProcessManager
    {
        IProcess MasterProcess { get; }

        void ForwardMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage);
    }

    internal class ProcessManager : IInternalProcessManager, IProcessManager
    {
        internal const string EndpointName = "ProcessManager";

        private readonly Dictionary<string, IProcessHandle> m_subprocesses = new Dictionary<string, IProcessHandle>(ProcessEndpointAddress.StringComparer);
        private readonly ITypeResolver m_typeResolver;

        public IProcess MasterProcess { get; }

        public ProcessManager(ITypeResolver resolver)
        {
            m_typeResolver = resolver;

            var proc = new Process("localhost", Process.MasterProcessUniqueId, m_typeResolver);
            m_typeResolver.AddService<IProcess>(proc);
            proc.RegisterEndpoint<IProcessManager>(EndpointName, this);
            MasterProcess = proc;
        }

        void IInternalProcessManager.ForwardMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage)
        {
            if (ProcessEndpointAddress.StringComparer.Equals(wrappedMessage.Destination.TargetProcess, Process.MasterProcessUniqueId))
            {
                MasterProcess.HandleMessage(source, wrappedMessage);
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
            if (ProcessEndpointAddress.StringComparer.Equals(info.ProcessName, Process.MasterProcessUniqueId))
                throw new InvalidOperationException("The master process cannot be created this way");

            if (GetSubprocess(info.ProcessName, throwIfMissing: false) != null)
            {
                if(mustCreate)
                    throw new InvalidOperationException("The process already exists");
                return false;
            }

            var handle = CreateNewProcessHandle(info);

            try
            {
                lock(m_subprocesses)
                {
                    if (m_subprocesses.ContainsKey(info.ProcessName))
                    {
                        if (mustCreate)
                            throw new InvalidOperationException("The process already exists");
                        return false;
                    }

                    m_subprocesses.Add(info.ProcessName, handle);
                }

                await handle.CreateActualProcessAsync();

                return true;
            }
            catch
            {
                lock (m_subprocesses)
                {
                    if (m_subprocesses.TryGetValue(info.ProcessName, out var knownHandle) && ReferenceEquals(knownHandle, handle))
                    {
                        m_subprocesses.Remove(info.ProcessName);
                    }
                }

                handle.Dispose();
                throw;
            }
        }

        private IProcessHandle CreateNewProcessHandle(ProcessCreationInfo info)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> DestroyProcess(string processName)
        {
            if (ProcessEndpointAddress.StringComparer.Equals(processName, Process.MasterProcessUniqueId))
                throw new InvalidOperationException("The master process cannot be destroyed this way");

            var target = GetSubprocess(processName, throwIfMissing: false);
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
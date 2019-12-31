#if WINDOWS_BUILD

using Spfx.Utilities.Interop;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes
{
    internal class WindowsProcessSpawnPunchHandles : IRemoteProcessInitializer
    {
        protected Process TargetProcess { get; private set; }

        private Win32ForeignProcessHandle m_duplicatedHandleForRemote;
        private NamelessNamedPipePair m_pipePair;

        Stream IRemoteProcessInitializer.ReadStream => m_pipePair.LocalPipe;
        Stream IRemoteProcessInitializer.WriteStream => m_pipePair.LocalPipe;

        public void Dispose()
        {
            DisposeAllHandles();
        }

        public virtual void InitializeInLock()
        {
        }

        public virtual void DisposeAllHandles()
        {
            m_pipePair?.LocalPipe.Dispose();
            m_pipePair?.RemoteProcessPipe.Dispose();
            m_duplicatedHandleForRemote?.Dispose();
        }

        public void HandleProcessCreatedInLock(Process targetProcess, ProcessSpawnPunchPayload initData)
        {
            TargetProcess = targetProcess;

            m_duplicatedHandleForRemote = Win32Interop.DuplicateHandleForOtherProcess(
                m_pipePair.RemoteProcessPipe,
                targetProcess,
                HandleAccessRights.GenericRead | HandleAccessRights.GenericWrite,
                inheritable: false);

            m_pipePair.RemoteProcessPipe.Dispose(); // no longer necessary

            initData.WritePipe = SafeHandleUtilities.SerializeHandle(m_duplicatedHandleForRemote.Value);
            initData.ReadPipe = null;

            var handleToThisProcess = Win32Interop.DuplicateHandleForOtherProcess(Win32Interop.ThisProcessPseudoHandle, TargetProcess, HandleAccessRights.Synchronize, inheritable: false)
                .ReleaseHandle();
            initData.ShutdownEvent = SafeHandleUtilities.SerializeHandle(handleToThisProcess);
        }

        public virtual string FinalizeInitDataAndSerialize(Process remoteProcess, ProcessSpawnPunchPayload initData)
        {
            m_duplicatedHandleForRemote.ReleaseHandle();
            return initData.SerializeToString();
        }

        ValueTask IRemoteProcessInitializer.CompleteHandshakeAsync(CancellationToken ct)
        {
            return default;
        }

        public async ValueTask InitializeAsync(CancellationToken ct)
        {
            m_pipePair = await NamelessNamedPipePair.CreatePair();
        }
    }
}

#endif
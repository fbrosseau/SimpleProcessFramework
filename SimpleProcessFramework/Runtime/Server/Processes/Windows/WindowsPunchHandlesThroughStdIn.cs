#if WINDOWS_BUILD

using Spfx.Utilities;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes.Windows
{
    internal class WindowsPunchHandlesThroughStdIn : AbstractWindowsPunchHandles
    {
        public override bool UsesStdIn => true;

        private NamelessNamedPipePair m_pipePair;

        protected override void OnDispose()
        {
            base.OnDispose();
            DisposeAllHandles();
        }

        public override void DisposeAllHandles()
        {
            base.DisposeAllHandles();
            m_pipePair?.LocalPipe.Dispose();
            m_pipePair?.RemoteProcessPipe.Dispose();
        }

        public override void HandleProcessCreatedAfterLock()
        {
            var duplicatedHandleForRemote = Win32Interop.DuplicateHandleForOtherProcess(
                m_pipePair.RemoteProcessPipe,
                Process,
                HandleAccessRights.GenericRead | HandleAccessRights.GenericWrite,
                inheritable: false);

            m_pipePair.RemoteProcessPipe.Dispose(); // no longer necessary

            InitData.WritePipe = SafeHandleUtilities.SerializeHandle(duplicatedHandleForRemote.ReleaseHandle());
            InitData.ReadPipe = null;

            InitData.ShutdownEvent = StringHandleToThisProcess;

            duplicatedHandleForRemote.ReleaseHandle();

            base.HandleProcessCreatedAfterLock();
        }

        public override async ValueTask InitializeAsync(ProcessSpawnPunchPayload initData, CancellationToken ct)
        {
            await base.InitializeAsync(initData, ct).ConfigureAwait(false);
            m_pipePair = await NamelessNamedPipePair.CreatePair().ConfigureAwait(false);
        }

        public override (Stream readStream, Stream writeStream) AcquireIOStreams()
        {
            var pair = m_pipePair;
            m_pipePair = null;
            return (pair.LocalPipe, pair.LocalPipe);
        }
    }
}

#endif
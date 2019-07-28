using Spfx.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes
{
    internal abstract class PipeBasedProcessSpawnPunchHandles : IRemoteProcessInitializer
    {
        protected Process TargetProcess { get; private set; }

        private bool m_disposeClientHandles;

        public AnonymousPipeServerStream ReadPipe { get; protected set; }
        public AnonymousPipeServerStream WritePipe { get; protected set; }

        Stream IRemoteProcessInitializer.ReadStream => ReadPipe;
        Stream IRemoteProcessInitializer.WriteStream => WritePipe;

        public void Dispose()
        {
            DisposeAllHandles();
        }

        public virtual void InitializeInLock()
        {
        }
               
        public virtual void DisposeAllHandles()
        {
            ReleaseRemoteProcessHandles();
            ReadPipe?.Dispose();
            WritePipe?.Dispose();
        }

        internal virtual void ReleaseRemoteProcessHandles()
        {
            DisposeClientHandle(ReadPipe);
            DisposeClientHandle(WritePipe);
        }

        public void HandleProcessCreatedInLock(Process targetProcess, ProcessSpawnPunchPayload initData)
        {
            TargetProcess = targetProcess;
            m_disposeClientHandles = targetProcess.Id != ProcessUtilities.CurrentProcessId;

            // those are inverted on purpose.
            initData.WritePipe = CreateHandleForOtherProcess(ReadPipe);
            initData.ReadPipe = CreateHandleForOtherProcess(WritePipe);
            initData.ShutdownEvent = SafeHandleUtilities.SerializeHandle(GetShutdownHandleForOtherProcess());
        }

        public virtual string FinalizeInitDataAndSerialize(Process remoteProcess, ProcessSpawnPunchPayload initData)
        {
            return initData.SerializeToString();
        }

        protected abstract IntPtr GetShutdownHandleForOtherProcess();

        private string CreateHandleForOtherProcess(AnonymousPipeServerStream stream)
        {
            string handle = stream.GetClientHandleAsString();
            DisposeClientHandle(stream);
            return handle;
        }

        private void DisposeClientHandle(AnonymousPipeServerStream stream)
        {
            if (m_disposeClientHandles)
                stream?.DisposeLocalCopyOfClientHandle();
        }

        Task IRemoteProcessInitializer.CompleteHandshakeAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
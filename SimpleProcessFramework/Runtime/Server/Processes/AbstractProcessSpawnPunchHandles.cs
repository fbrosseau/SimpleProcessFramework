using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes
{
    internal abstract class AbstractProcessSpawnPunchHandles : IProcessSpawnPunchHandles
    {
        public AnonymousPipeServerStream ReadPipe { get; protected set; }
        public AnonymousPipeServerStream WritePipe { get; protected set; }
        public Process RemoteProcess { get; private set; }

        Stream IProcessSpawnPunchHandles.ReadStream => ReadPipe;
        Stream IProcessSpawnPunchHandles.WriteStream => WritePipe;

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
            ReadPipe.Dispose();
            WritePipe.Dispose();
        }

        internal virtual void ReleaseRemoteProcessHandles()
        {
            ReadPipe.DisposeLocalCopyOfClientHandle();
            WritePipe.DisposeLocalCopyOfClientHandle();
        }

        public void HandleProcessCreatedInLock(Process targetProcess, ProcessSpawnPunchPayload initData)
        {
            // those are inverted on purpose.
            initData.WritePipe = CreateHandleForOtherProcess(ReadPipe);
            initData.ReadPipe = CreateHandleForOtherProcess(WritePipe);
            initData.ShutdownEvent = ProcessSpawnPunchPayload.SerializeHandle(GetShutdownHandleForOtherProcess(targetProcess));
        }

        public virtual string FinalizeInitDataAndSerialize(Process remoteProcess, ProcessSpawnPunchPayload initData)
        {
            return initData.SerializeToString();
        }

        protected abstract IntPtr GetShutdownHandleForOtherProcess(Process remoteProcess);

        private string CreateHandleForOtherProcess(AnonymousPipeServerStream stream)
        {
            string handle = stream.GetClientHandleAsString();
            stream.DisposeLocalCopyOfClientHandle();
            return handle;
        }

        Task IProcessSpawnPunchHandles.CompleteHandshakeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
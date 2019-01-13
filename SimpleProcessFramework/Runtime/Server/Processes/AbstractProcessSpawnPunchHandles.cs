using System;
using System.Diagnostics;
using System.IO.Pipes;

namespace SimpleProcessFramework.Runtime.Server.Processes
{
    internal abstract class AbstractProcessSpawnPunchHandles : IDisposable
    {
        public AnonymousPipeServerStream ReadPipe { get; protected set; }
        public AnonymousPipeServerStream WritePipe { get; protected set; }
        public Process RemoteProcess { get; private set; }

        public void Dispose()
        {
            DisposeAllHandles();
        }

        internal virtual void DisposeAllHandles()
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

        internal virtual string FinalizeInitDataAndSerialize(Process remoteProcess, ProcessSpawnPunchPayload initData)
        {
            // those are inverted on purpose.
            initData.WritePipe = CreateHandleForOtherProcess(ReadPipe);
            initData.ReadPipe = CreateHandleForOtherProcess(WritePipe);
            return initData.SerializeToString();
        }

        private string CreateHandleForOtherProcess(AnonymousPipeServerStream stream)
        {
            string handle = stream.GetClientHandleAsString();
            stream.DisposeLocalCopyOfClientHandle();
            return handle;
        }
    }
}
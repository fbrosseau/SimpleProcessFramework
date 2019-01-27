using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace Spfx.Runtime.Server.Processes
{
    internal class WindowsProcessSpawnPunchHandles : AbstractProcessSpawnPunchHandles
    {
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(IntPtr srcProcess, SafeHandle srcHandle, IntPtr targetProcess, out IntPtr targetHandle, uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(IntPtr srcProcess, IntPtr srcHandle, SafeHandle targetProcess, out IntPtr targetHandle, uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        public WindowsProcessSpawnPunchHandles()
        {
            ReadPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            WritePipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        }

        protected override IntPtr GetShutdownHandleForOtherProcess(Process remoteProcess)
        {
            const int SYNCHRONIZE = 0x00100000;
            DuplicateHandle((IntPtr)(-1), (IntPtr)(-1), remoteProcess.SafeHandle, out var result, SYNCHRONIZE, false, 0);
            return result;
        }
    }

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
            initData.ShutdownEvent = ProcessSpawnPunchPayload.SerializeHandle(GetShutdownHandleForOtherProcess(remoteProcess));
            return initData.SerializeToString();
        }

        protected abstract IntPtr GetShutdownHandleForOtherProcess(Process remoteProcess);

        private string CreateHandleForOtherProcess(AnonymousPipeServerStream stream)
        {
            string handle = stream.GetClientHandleAsString();
            stream.DisposeLocalCopyOfClientHandle();
            return handle;
        }
    }
}
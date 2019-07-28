using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace Spfx.Runtime.Server.Processes
{
    internal class WindowsProcessSpawnPunchHandles : PipeBasedProcessSpawnPunchHandles
    {
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(IntPtr srcProcess, IntPtr srcHandle, SafeHandle targetProcess, out IntPtr targetHandle, uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwOptions);

        public override void InitializeInLock()
        {
            ReadPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            WritePipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
            base.InitializeInLock();
        }

        protected override IntPtr GetShutdownHandleForOtherProcess()
        {
            const int SYNCHRONIZE = 0x00100000;
            if (!DuplicateHandle((IntPtr)(-1), (IntPtr)(-1), TargetProcess.SafeHandle, out var result, SYNCHRONIZE, false, 0))
                throw new Win32Exception();
            return result;
        }
    }
}
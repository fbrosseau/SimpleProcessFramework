using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Spfx.Utilities.Interop
{
    [Flags]
    internal enum HandleAccessRights
    {
        Synchronize = 0x00100000,
        GenericRead,
        GenericWrite
    }

    internal static class Win32Interop
    {
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(IntPtr srcProcess, IntPtr srcHandle, SafeHandle targetProcess, out IntPtr targetHandle, uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(SafeHandle srcProcess, IntPtr srcHandle, IntPtr targetProcess, out SafeFileHandle targetHandle, uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(SafeHandle srcProcess, SafeHandle srcHandle, IntPtr targetProcess, out SafeFileHandle targetHandle, uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(IntPtr srcProcess, SafeHandle srcHandle, SafeHandle targetProcess, out IntPtr targetHandle, uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwOptions);

        [DllImport("api-ms-win-core-processthreads-l1-1-2", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("api-ms-win-core-file-l1-2-0", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string fileName, int access, FileShare share, IntPtr securityAttributes, FileMode creationDisposition, int flagsAndAttributes, IntPtr template);
        [DllImport("api-ms-win-core-errorhandling-l1-1-3", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetThreadErrorMode(int newMode, out int oldMode);

        private const int DUPLICATE_CLOSE_SOURCE = 1;

        public static readonly IntPtr ThisProcessPseudoHandle = GetCurrentProcess();

        internal static Win32ForeignProcessHandle DuplicateHandleForOtherProcess(IntPtr srcHandle, Process targetProcess, HandleAccessRights newAccessRights, bool inheritable)
        {
            return DuplicateHandleForOtherProcess(srcHandle, targetProcess.SafeHandle, newAccessRights, inheritable);
        }
        
        internal static Win32ForeignProcessHandle DuplicateHandleForOtherProcess(SafeHandle srcHandle, Process targetProcess, HandleAccessRights newAccessRights, bool inheritable)
        {
            if (!DuplicateHandle(ThisProcessPseudoHandle, srcHandle, targetProcess.SafeHandle, out var result, (uint)newAccessRights, inheritable, 0))
                throw new Win32Exception();
            return new Win32ForeignProcessHandle(targetProcess, result);
        }

        internal static Win32ForeignProcessHandle DuplicateHandleForOtherProcess(IntPtr srcHandle, SafeHandle targetProcess, HandleAccessRights newAccessRights, bool inheritable)
        {
            if (!DuplicateHandle(ThisProcessPseudoHandle, srcHandle, targetProcess, out var result, (uint)newAccessRights, inheritable, 0))
                throw new Win32Exception();
            return new Win32ForeignProcessHandle(targetProcess, result);
        }

        internal static void CloseHandleInRemoteProcess(SafeHandle owningProcess, IntPtr handle)
        {
            DuplicateHandle(owningProcess, handle, ThisProcessPseudoHandle, out var dummy, 0, false, DUPLICATE_CLOSE_SOURCE);
            dummy?.Dispose();
        }

        internal static SafeHandle SafeCreateFile(string name, FileAccess access, FileShare share, FileMode mode, bool async)
        {
            const int SEM_FAILCRITICALERRORS = 1;
            SetThreadErrorMode(SEM_FAILCRITICALERRORS, out var lastMode);
            try
            {
                int rawAccess = 0;
                int options = 0;

                const int SECURITY_SQOS_PRESENT = 0x00100000;
                const int OVERLAPPED = 0x40000000;
                const int SECURITY_ANONYMOUS = 0;
                const int GENERIC_READ = unchecked((int)0x80000000);
                const int GENERIC_WRITE = 0x40000000;

                options = SECURITY_SQOS_PRESENT | SECURITY_ANONYMOUS;
                if (async)
                    options |= OVERLAPPED;

                if ((access & FileAccess.Read) == FileAccess.Read)
                    rawAccess |= GENERIC_READ;
                if ((access & FileAccess.Write) == FileAccess.Write)
                    rawAccess |= GENERIC_WRITE;

                return CreateFile(name, rawAccess, share, IntPtr.Zero, mode, options, IntPtr.Zero);
            }
            finally
            {
                SetThreadErrorMode(lastMode, out _);
            }
        }
    }
}

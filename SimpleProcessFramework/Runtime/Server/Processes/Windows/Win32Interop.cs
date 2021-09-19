#if WINDOWS_BUILD

using Microsoft.Win32.SafeHandles;
using Spfx.Utilities;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Spfx.Runtime.Server.Processes.Windows
{
    [Flags]
    internal enum HandleAccessRights : uint
    {
        Synchronize = 0x00100000,
        GenericRead = 0x80000000,
        GenericWrite = 0x40000000,

        ReadControl = 0x00020000,

        StandardRightsRead = ReadControl,
        FileReadData = 1,
        FileReadEa = 0x8,

        StandardRightsWrite = ReadControl,
        FileWriteData = 2,
        FileWriteEa = 0x10,
        FileAppendData = 4,

        FileGenericRead = StandardRightsRead | FileReadData | FileReadEa | Synchronize,
        FileGenericWrite = StandardRightsWrite | FileWriteData | FileWriteEa | FileAppendData | Synchronize
    }

    [Flags]
    public enum ProcessCreationFlags
    {
        DETACHED_PROCESS = 8,
        EXTENDED_STARTUPINFO_PRESENT = 0x00080000,
        CREATE_SUSPENDED = 4,
        CREATE_UNICODE_ENVIRONMENT = 0x00000400,
    }

    [Flags]
    internal enum StartupInfoFlags
    {
        FORCEONFEEDBACK = 0x00000040,
        FORCEOFFFEEDBACK = 0x00000080,
        PREVENTPINNING = 0x00002000,
        RUNFULLSCREEN = 0x00000020,
        TITLEISAPPID = 0x00001000,
        TITLEISLINKNAME = 0x00000800,
        UNTRUSTEDSOURCE = 0x00008000,
        USECOUNTCHARS = 0x00000008,
        USEFILLATTRIBUTE = 0x00000010,
        USEHOTKEY = 0x00000200,
        USEPOSITION = 0x00000004,
        USESHOWWINDOW = 0x00000001,
        USESIZE = 0x00000002,
        USESTDHANDLES = 0x00000100
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal class STARTUPINFOEX
    {
        /* STARTUPINFO */
        public int cb = Marshal.SizeOf<STARTUPINFOEX>();
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public StartupInfoFlags dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public SafeHandle hStdInput = SafeHandleUtilities.NullSafeHandle;
        public SafeHandle hStdOutput = SafeHandleUtilities.NullSafeHandle;
        public SafeHandle hStdError = SafeHandleUtilities.NullSafeHandle;
        /* End STARTUPINFO */

        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    internal enum StdHandle
    {
        STD_INPUT_HANDLE = -10,
        STD_OUTPUT_HANDLE = -11,
        STD_ERROR_HANDLE = -12
    }

    internal static class Win32Interop
    {
        public static class ConsoleHandles
        {
            public static readonly NoDisposeSafeHandle DefaultStdIn = new NoDisposeSafeHandle(GetStdHandle(StdHandle.STD_INPUT_HANDLE));
            public static readonly NoDisposeSafeHandle DefaultStdOut = new NoDisposeSafeHandle(GetStdHandle(StdHandle.STD_OUTPUT_HANDLE));
            public static readonly NoDisposeSafeHandle DefaultStdErr = new NoDisposeSafeHandle(GetStdHandle(StdHandle.STD_ERROR_HANDLE));
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LogonUser(
            [MarshalAs(UnmanagedType.LPWStr)]string lpszUsername,
            [MarshalAs(UnmanagedType.LPWStr)]string lpszDomain,
            IntPtr passwordBytes,
            int dwLogonType,
            int dwLogonProvider,
            out SafeFileHandle phToken);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LogonUser(
            [MarshalAs(UnmanagedType.LPWStr)]string lpszUsername,
            [MarshalAs(UnmanagedType.LPWStr)]string lpszDomain,
            [MarshalAs(UnmanagedType.LPWStr)]string password,
            int dwLogonType,
            int dwLogonProvider,
            out SafeFileHandle phToken);

        [DllImport("api-ms-win-core-processthreads-l1-1-2", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcessAsUser(
            SafeHandle userToken,
            [In] string lpApplicationName,
            [In] string lpCommandLine,
            /*[In] ref SECURITY_ATTRIBUTES*/IntPtr lpProcessAttributes,
            /*[In] ref SECURITY_ATTRIBUTES*/IntPtr lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            ProcessCreationFlags dwCreationFlags,
            [In] string lpEnvironment,
            [In] string lpCurrentDirectory,
            [In] STARTUPINFOEX lpStartupInfo,
            [Out] out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("api-ms-win-core-processthreads-l1-1-2", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateProcess(
            [In] string lpApplicationName,
            [In] byte[] lpCommandLine,
            /*[In] ref SECURITY_ATTRIBUTES*/IntPtr lpProcessAttributes,
            /*[In] ref SECURITY_ATTRIBUTES*/IntPtr lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            ProcessCreationFlags dwCreationFlags,
            [In] byte[] lpEnvironment,
            [In] string lpCurrentDirectory,
            [In] STARTUPINFOEX lpStartupInfo,
            [Out] out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("ms-win-downlevel-shell32-l1-1-0", SetLastError = true, CharSet = CharSet.Unicode)]
        private static unsafe extern IntPtr CommandLineToArgvW(string cmdline, out int numArgs);

        [DllImport("api-ms-win-core-heap-l2-1-0")]
        private static extern IntPtr LocalFree(IntPtr addr);

        public static unsafe string[] CommandLineToArgs(string cmdline)
        {
            var res = CommandLineToArgvW(cmdline, out int count);
            if (res == IntPtr.Zero)
                throw new Win32Exception();

            var results = new string[count];

            try
            {
                for (int i = 0; i < count; ++i)
                {
                    results[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(res, IntPtr.Size * i));
                }

                return results;
            }
            finally
            {
                LocalFree(res);
            }
        }

        [DllImport("api-ms-win-core-processthreads-l1-1-2", SetLastError = true)]
        public static extern bool InitializeProcThreadAttributeList(SafeHandle lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);
        [DllImport("api-ms-win-core-processthreads-l1-1-2", SetLastError = true)]
        public static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);
        [DllImport("api-ms-win-core-processthreads-l1-1-2", SetLastError = true)]
        public static extern bool UpdateProcThreadAttribute(SafeHandle lpAttributeList, uint dwFlags, IntPtr Attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);
        [DllImport("api-ms-win-core-processthreads-l1-1-2", SetLastError = true)]
        public static extern void DeleteProcThreadAttributeList(SafeHandle lpAttributeList);

        [DllImport("api-ms-win-core-processthreads-l1-1-2", SetLastError = true)]
        public static extern int ResumeThread(SafeHandle hThread);

        [DllImport("api-ms-win-core-processenvironment-l1-2-0", SetLastError = true)]
        internal static extern IntPtr GetStdHandle(StdHandle whichHandle);
        [DllImport("api-ms-win-core-processenvironment-l1-2-0", SetLastError = true)]
        internal static extern bool SetStdHandle(StdHandle whichHandle, SafeHandle h);

        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(IntPtr srcProcess, IntPtr srcHandle, SafeHandle targetProcess, out IntPtr targetHandle, int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(IntPtr srcProcess, IntPtr srcHandle, IntPtr targetProcess, out IntPtr targetHandle, int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(IntPtr srcProcess, SafeHandle srcHandle, IntPtr targetProcess, out SafeFileHandle targetHandle, int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(IntPtr srcProcess, SafeHandle srcHandle, IntPtr targetProcess, out IntPtr targetHandle, int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(SafeHandle srcProcess, IntPtr srcHandle, IntPtr targetProcess, out SafeFileHandle targetHandle, int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(SafeHandle srcProcess, SafeHandle srcHandle, IntPtr targetProcess, out SafeFileHandle targetHandle, int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(IntPtr srcProcess, SafeHandle srcHandle, SafeHandle targetProcess, out IntPtr targetHandle, int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(SafeHandle srcProcess, SafeHandle srcHandle, SafeHandle targetProcess, out IntPtr targetHandle, int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(SafeHandle srcProcess, SafeHandle srcHandle, SafeHandle targetProcess, out SafeFileHandle targetHandle, int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(IntPtr srcProcess, IntPtr srcHandle, IntPtr targetProcess, out SafeFileHandle targetHandle, int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        private static extern bool DuplicateHandle(SafeHandle srcProcess, IntPtr srcHandle, SafeHandle targetProcess, out SafeFileHandle targetHandle, int dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwOptions);
        [DllImport("api-ms-win-core-handle-l1-1-0", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr handle);
        [DllImport("api-ms-win-core-processthreads-l1-1-2", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("api-ms-win-core-file-l1-2-0", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string fileName, HandleAccessRights access, FileShare share, IntPtr securityAttributes, FileMode creationDisposition, int flagsAndAttributes, IntPtr template);
        [DllImport("api-ms-win-core-errorhandling-l1-1-3", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetThreadErrorMode(int newMode, out int oldMode);

        private const int DUPLICATE_CLOSE_SOURCE = 1;
        private const int DUPLICATE_SAME_ACCESS = 2;

        public static readonly IntPtr ThisProcessPseudoHandleValue = GetCurrentProcess();
        public static readonly NoDisposeSafeHandle ThisProcessPseudoHandle = new NoDisposeSafeHandle(ThisProcessPseudoHandleValue);

        internal static SafeHandle DuplicateLocalProcessHandle(IntPtr srcHandle, bool inheritable, HandleAccessRights? newAccessRights = null)
        {
            var (accessRights, flags) = GetDuplicateRightsAndOptions(newAccessRights);
            if (!DuplicateHandle(ThisProcessPseudoHandleValue, srcHandle, ThisProcessPseudoHandleValue, out SafeFileHandle h, accessRights, inheritable, flags))
                throw new Win32Exception();
            return h;
        }

        internal static void DuplicateLocalProcessHandle(SafeHandle srcHandle, bool inheritable, out IntPtr handle, HandleAccessRights? newAccessRights = null)
        {
            var (accessRights, flags) = GetDuplicateRightsAndOptions(newAccessRights);
            if (!DuplicateHandle(ThisProcessPseudoHandleValue, srcHandle, ThisProcessPseudoHandleValue, out handle, accessRights, inheritable, flags))
                throw new Win32Exception();
        }

        internal static SafeHandle DuplicateLocalProcessHandle(SafeHandle srcHandle, bool inheritable, HandleAccessRights? newAccessRights = null)
        {
            var (accessRights, flags) = GetDuplicateRightsAndOptions(newAccessRights);
            if (!DuplicateHandle(ThisProcessPseudoHandleValue, srcHandle, ThisProcessPseudoHandleValue, out SafeFileHandle h, accessRights, inheritable, flags))
                throw new Win32Exception();
            return h;
        }

        private static (int accessrights, int flags) GetDuplicateRightsAndOptions(HandleAccessRights? newAccessRights)
        {
            if (newAccessRights is null)
                return (0, DUPLICATE_SAME_ACCESS);
            else
                return (unchecked((int)newAccessRights), 0);
        }

        internal static Win32ForeignProcessHandle DuplicateHandleForOtherProcess(IntPtr srcHandle, Process targetProcess, HandleAccessRights newAccessRights, bool inheritable)
        {
            return DuplicateHandleForOtherProcess(srcHandle, targetProcess.SafeHandle, newAccessRights, inheritable);
        }

        internal static Win32ForeignProcessHandle DuplicateHandleForOtherProcess(SafeHandle srcHandle, Process targetProcess, HandleAccessRights newAccessRights, bool inheritable)
        {
            return DuplicateHandleForOtherProcess(srcHandle, targetProcess.SafeHandle, newAccessRights, inheritable);
        }

        internal static Win32ForeignProcessHandle DuplicateHandleForOtherProcess(SafeHandle srcHandle, SafeHandle targetProcess, HandleAccessRights newAccessRights, bool inheritable)
        {
            if (!DuplicateHandle(ThisProcessPseudoHandle, srcHandle, targetProcess, out IntPtr result, (int)newAccessRights, inheritable, 0))
                throw new Win32Exception();
            return new Win32ForeignProcessHandle(targetProcess, result);
        }

        internal static Win32ForeignProcessHandle DuplicateHandleForOtherProcess(IntPtr srcHandle, SafeHandle targetProcess, HandleAccessRights newAccessRights, bool inheritable)
        {
            if (!DuplicateHandle(ThisProcessPseudoHandleValue, srcHandle, targetProcess, out IntPtr result, (int)newAccessRights, inheritable, 0))
                throw new Win32Exception();
            return new Win32ForeignProcessHandle(targetProcess, result);
        }

        internal static void CloseHandleInRemoteProcess(SafeHandle owningProcess, IntPtr handle)
        {
            DuplicateHandle(owningProcess, handle, ThisProcessPseudoHandleValue, out SafeFileHandle dummy, 0, false, DUPLICATE_CLOSE_SOURCE);
            dummy?.Dispose();
        }

        internal static SafeHandle SafeCreateFile(string name, FileAccess access, FileShare share, FileMode mode, bool async)
        {
            const int SEM_FAILCRITICALERRORS = 1;
            SetThreadErrorMode(SEM_FAILCRITICALERRORS, out var lastMode);
            try
            {
                HandleAccessRights rawAccess = 0;
                int options = 0;

                const int SECURITY_SQOS_PRESENT = 0x00100000;
                const int OVERLAPPED = 0x40000000;
                const int SECURITY_ANONYMOUS = 0;

                options = SECURITY_SQOS_PRESENT | SECURITY_ANONYMOUS;
                if (async)
                    options |= OVERLAPPED;

                if ((access & FileAccess.Read) == FileAccess.Read)
                    rawAccess |= HandleAccessRights.GenericRead;
                if ((access & FileAccess.Write) == FileAccess.Write)
                    rawAccess |= HandleAccessRights.GenericWrite;

                return CreateFile(name, rawAccess, share, IntPtr.Zero, mode, options, IntPtr.Zero);
            }
            finally
            {
                SetThreadErrorMode(lastMode, out _);
            }
        }
    }
}

#endif
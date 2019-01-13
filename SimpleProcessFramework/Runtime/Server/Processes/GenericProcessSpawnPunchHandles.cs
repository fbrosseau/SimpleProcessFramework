using System.IO;
using System.Threading.Tasks;
using System.IO.Pipes;

namespace SimpleProcessFramework.Runtime.Server.Processes
{
    internal sealed class GenericProcessSpawnPunchHandles : AbstractProcessSpawnPunchHandles
    {
        public GenericProcessSpawnPunchHandles()
        {
            ReadPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            WritePipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        }
    }
}

#if patate
#if WINDOWS_BUILD

using Genetec.ServiceModel.ServiceInterfaces;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Genetec.ServiceModel.Host;
using System.Threading.Tasks;
using Genetec.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using System.Threading;
using Genetec.Diagnostics.Logging;
using Genetec.Interop;
using Genetec.Interop.PInvoke;
using Genetec.Security.AccessControl;
using Genetec.Shell;
using Genetec.Utils;
using static Genetec.Interop.PInvoke.WindowsAPI;

namespace Genetec.ServiceModel.Publishing.Core.DomainHandles
{
    internal class WindowsProcessServiceDomainHandle : ProcessServiceDomainHandle
    {
        public static bool AlwaysCreateProcessesSuspended { get; set; }

        protected override async Task<RemoteServiceDomainInitializer> CreateTargetProcess(ServiceDomainCreationInfo creationInfo, ServiceDomainInitializationData initData)
        {
            m_strategy = creationInfo.LoadingStrategy;

            if (creationInfo.ProcessInfo == null)
                creationInfo.ProcessInfo = new ServiceDomainProcessInformation();
            var processInfo = creationInfo.ProcessInfo;

            /* those are the things that absolutely need proper dispose */
            var createdProcessInfo = new PROCESS_INFORMATION();
            GCHandle tempMemoryHandle = new GCHandle();
            bool everythingSucceeded = false;
            Process createdProcess = null;
            bool hasProcessLock = false;
            WindowsRemoteServiceDomainInitializer remoteProcessInitializer = null;
            byte[] procThreadAttributeListToCleanup = null;

            processInfo.ProcessName = GetProcessPath(processInfo.ProcessName, creationInfo.LoadingStrategy);
            processInfo.ApplyDefaultValues();

            try
            {
                SafeCloseWin32Handle loginToken;
                LogonIfNeeded(processInfo, out loginToken);

                Logger.Info?.Trace(new TraceEntry("Spawning remote process {0}...", processInfo.ProcessName)
                    .SetDetailedMessage("{0}", processInfo));

                Monitor.Enter(SafeProcessStart.ProcessCreationLock, ref hasProcessLock);

                // inheritable, but we're in the lock.
                remoteProcessInitializer = new WindowsRemoteServiceDomainInitializer(inheritableHandles: true);

                // forceofffeedback: never show a spinning cursor during the boot phase of the subprocess!
                var startFlags = StartupInfoFlags.USESTDHANDLES | StartupInfoFlags.FORCEOFFFEEDBACK;

                switch (processInfo.ProcessPriority)
                {
                    case ServiceDomainProcessPriority.BelowNormal:
                        startFlags |= (StartupInfoFlags)0x4000; /*BELOW_NORMAL_PRIORITY_CLASS*/
                        break;
                    case ServiceDomainProcessPriority.Normal:
                        startFlags |= (StartupInfoFlags)0x20; /*NORMAL_PRIORITY_CLASS*/
                        break;
                    case ServiceDomainProcessPriority.AboveNormal:
                        startFlags |= (StartupInfoFlags)0x8000; /*ABOVE_NORMAL_PRIORITY_CLASS*/
                        break;
                }

                STARTUPINFOEX startupinfo = new STARTUPINFOEX
                {
                    StartupInfo =
                    {
                        cb = Marshal.SizeOf(typeof(STARTUPINFOEX)),
                        dwFlags = startFlags,
                        hStdInput = remoteProcessInitializer.PrimaryPipe.ClientSafePipeHandle,
                        hStdOutput = new SafeFileHandle(GetStdHandle(-11) ,false),
                        hStdError = new SafeFileHandle(GetStdHandle(-12) ,false)
                    }
                };

                IntPtr requiredAttrListSize = IntPtr.Zero;
                if (!InitializeProcThreadAttributeList(null, 1, 0, ref requiredAttrListSize)
                    && Marshal.GetLastWin32Error() != (int)WinError.INSUFFICIENT_BUFFER)
                    ThrowWin32Exception("InitializeProcThreadAttributeList");
                var attrList = new byte[requiredAttrListSize.ToInt32()];
                if (!InitializeProcThreadAttributeList(attrList, 1, 0, ref requiredAttrListSize))
                    ThrowWin32Exception("InitializeProcThreadAttributeList");
                procThreadAttributeListToCleanup = attrList;

                IntPtr PROC_THREAD_ATTRIBUTE_HANDLE_LIST = (IntPtr)0x00020002;

                IntPtr[] handlesToInherit = { remoteProcessInitializer.PrimaryPipe.ClientSafePipeHandle.DangerousGetHandle() };
                if (!UpdateProcThreadAttribute(attrList, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST, handlesToInherit, (IntPtr)(handlesToInherit.Length * IntPtr.Size), IntPtr.Zero, IntPtr.Zero))
                    ThrowWin32Exception("UpdateProcThreadAttribute(PROC_THREAD_ATTRIBUTE_HANDLE_LIST)");

                tempMemoryHandle = GCHandle.Alloc(attrList, GCHandleType.Pinned);
                startupinfo.lpAttributeList = tempMemoryHandle.AddrOfPinnedObject();

                string cmdLine = $"\"{processInfo.ProcessName}\" \"{creationInfo.ServiceDomainId}\" {ProcessHelper.CurrentProcessId}";

                if (LoadingStrategy.IsNetCore())
                {
                    cmdLine = "\"" + GetDotNetCorePath(LoadingStrategy) + "\" " + cmdLine;
                }

                TimeSpan createSuspendedDelay = TimeSpan.Zero;
                ProcessCreationFlags flags = /*ProcessCreationFlags.DETACHED_PROCESS |*/ ProcessCreationFlags.EXTENDED_STARTUPINFO_PRESENT | ProcessCreationFlags.CREATE_UNICODE_ENVIRONMENT;

                if (processInfo.MaxMachineCpuPercent == 0)
                    processInfo.MaxMachineCpuPercent = ServiceModelSettings.Default.DefaultMaxSubprocessCpuRate;

                bool requiresJobObject = processInfo.MaxMachineCpuPercent != 0
                                         || processInfo.MaxProcessMemoryBytes != 0
                                         || processInfo.PreventSubprocessCreation;

                bool createSuspended = createSuspendedDelay > TimeSpan.Zero || requiresJobObject || AlwaysCreateProcessesSuspended;

                if (createSuspended)
                {
                    flags |= ProcessCreationFlags.CREATE_SUSPENDED;
                }

                var env = Environment.GetEnvironmentVariables();
                ConfigureIntegrityLevel(processInfo, env, ref loginToken);

                var environmentBlockBuilder = new StringBuilder();
                foreach (DictionaryEntry kvp in env)
                {
                    environmentBlockBuilder.AppendFormat("{0}={1}\0", kvp.Key, kvp.Value);
                }

                bool success;
                if (loginToken?.IsInvalid == false)
                {
                    success = CreateProcessAsUser(
                        loginToken,
                        lpApplicationName: null,
                        lpCommandLine: cmdLine,
                        lpProcessAttributes: IntPtr.Zero,
                        lpThreadAttributes: IntPtr.Zero,
                        bInheritHandles: true,
                        dwCreationFlags: flags,
                        lpEnvironment: environmentBlockBuilder.ToString(),
                        lpCurrentDirectory: ExecutionPath.Value,
                        lpStartupInfo: ref startupinfo,
                        lpProcessInformation: out createdProcessInfo);
                    if (!success)
                        ThrowWin32Exception("CreateProcessAsUser");
                }
                else
                {
                    success = CreateProcess(
                        lpApplicationName: null,
                        lpCommandLine: cmdLine,
                        lpProcessAttributes: IntPtr.Zero,
                        lpThreadAttributes: IntPtr.Zero,
                        bInheritHandles: true,
                        dwCreationFlags: flags,
                        lpEnvironment: environmentBlockBuilder.ToString(),
                        lpCurrentDirectory: ExecutionPath.Value,
                        lpStartupInfo: ref startupinfo,
                        lpProcessInformation: out createdProcessInfo);
                    if (!success)
                        ThrowWin32Exception("CreateProcess");
                }

                Monitor.Exit(SafeProcessStart.ProcessCreationLock);
                hasProcessLock = false;

                // the primary pipe has been duplicated at this point
                remoteProcessInitializer.PrimaryPipe.DisposeLocalCopyOfClientHandle();

                createdProcess = Process.GetProcessById(createdProcessInfo.dwProcessId);
                remoteProcessInitializer.SetRemoteProcess(createdProcess);

                // create the host process
                m_remoteHostProcess = createdProcess;
                m_remoteHostProcess.EnableRaisingEvents = true;
                m_remoteHostProcess.Exited += (sender, args) => OnConnectionLost();

                if (m_remoteHostProcess.HasExited)
                {
                    // it's OK to call it twice in case of race
                    OnConnectionLost();
                }

                if (createSuspendedDelay > TimeSpan.Zero)
                {
                    await Task.Delay(createSuspendedDelay).NoCapture();
                }

                if (requiresJobObject)
                    ApplyJobObjectRestrictions(creationInfo.ProcessInfo, createdProcessInfo.hProcess);

                if (createSuspended)
                {
                    if (ResumeThread(createdProcessInfo.hThread) == -1)
                        ThrowWin32Exception("ResumeThread");
                }

                Logger.Debug?.Trace("FinishClientHandlesTransfer...");
                remoteProcessInitializer.FinishClientHandlesTransfer(initData);
                everythingSucceeded = true;
                return remoteProcessInitializer;
            }
            catch (Exception ex)
            {
                lock (s_createdCustomProcesses)
                {
                    s_createdCustomProcesses.Remove(GetProcessKey(processInfo.ProcessName, creationInfo.LoadingStrategy));
                }

                Logger.Warning?.Trace(ex, "Exception in CreateTargetDomain");

                throw;
            }
            finally
            {
                if (!everythingSucceeded)
                {
                    remoteProcessInitializer?.DisposeAllHandles();

                    if (hasProcessLock)
                    {
                        Monitor.Exit(SafeProcessStart.ProcessCreationLock);
                    }

                    try
                    {
                        createdProcess?.Kill();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning?.Trace(ex, "Failed to destroy the created process");
                    }
                }

                if (createdProcessInfo.hProcess != IntPtr.Zero)
                    CloseHandle(createdProcessInfo.hProcess);
                if (createdProcessInfo.hThread != IntPtr.Zero)
                    CloseHandle(createdProcessInfo.hThread);
                if (procThreadAttributeListToCleanup != null)
                    DeleteProcThreadAttributeList(procThreadAttributeListToCleanup);
                if (tempMemoryHandle.IsAllocated)
                    tempMemoryHandle.Free(); // this must be after the above Delete
            }
        }

        private void ApplyJobObjectRestrictions(ServiceDomainProcessInformation processInfo, IntPtr hProcess)
        {
            if (!JobObjectUtilities.CanAddProcessToJobObject(hProcess))
            {
                Logger.Warning?.Trace("This version of Windows does not support Nested Job Objects. The process restrictions will not be applied.");
                return;
            }

            var jobObject = JobObjectUtilities.CreateNewJobObject();

            AddDisposableMember(jobObject);
            JobObjectUtilities.AddProcessToJob(jobObject, hProcess);

            if (!JobObjectUtilities.SetCpuLimit(jobObject, processInfo.MaxMachineCpuPercent, throwOnError: false))
                Logger.Warning?.Trace("Failed to set CPU cap on process {0}", m_remoteHostProcess.Id);
            if (!JobObjectUtilities.SetMemoryLimit(jobObject, processInfo.MaxProcessMemoryBytes, throwOnError: false))
                Logger.Warning?.Trace("Failed to set memory cap on process {0}", m_remoteHostProcess.Id);

            Logger.Debug?.Trace("Job Object restrictions have been set");
        }

        private void ConfigureIntegrityLevel(ServiceDomainProcessInformation creationInfo, IDictionary env, ref SafeCloseWin32Handle processLogonToken)
        {
            if (creationInfo.IntegrityLevel != ServiceDomainIntegrityLevel.Low)
                return;

            Logger.Info?.Trace("This request requires Low integrity");

            if (EnsureHasLowIntegrityTempFolder(out string tmpPath))
            {
                Logger.Info?.Trace("The subprocess will use \"{0}\" as its temp folder", tmpPath);
                env["TEMP"] = tmpPath;
                env["TMP"] = tmpPath;
            }

            MandatoryLabelUtilities.DuplicateTokenAndChangeIntegrityLevel(ref processLogonToken, ObjectIntegrityLevel.Low, releaseInputToken: true);
        }

        private bool EnsureHasLowIntegrityTempFolder(out string tmp)
        {
            tmp = Path.Combine(Path.GetTempPath(), "Low");
            if (Directory.Exists(tmp))
                return true;

            try
            {
                Directory.CreateDirectory(tmp);
                MandatoryLabelUtilities.ChangeFileMandatoryLabel(tmp, ObjectIntegrityLevel.Low);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Debug?.Trace(ex, "Failed to create the Low Integrity temp folder");
                return false;
            }
        }

        private void LogonIfNeeded(ServiceDomainProcessInformation creationInfo, out SafeCloseWin32Handle impersonateLoginUserToken)
        {
            if (string.IsNullOrWhiteSpace(creationInfo?.UserName))
            {
                // if there's a password, we'll do a CreateProcessWithLogonW instead, so no token needed here
                impersonateLoginUserToken = null;
                return;
            }

            string username = creationInfo.UserName;
            string domain = null;
            var parts = username.Split('\\');
            var loginType = 0; //default

            const int LOGON32_LOGON_SERVICE = 5;

            if (parts.Length == 2)
            {
                username = parts[1];
                domain = parts[0];
                if (domain == "NT AUTHORITY")
                {
                    loginType = LOGON32_LOGON_SERVICE;
                }
            }


#if DEBUG
            if (loginType == LOGON32_LOGON_SERVICE && !AttemptServiceLogonInDebug)
            {
                impersonateLoginUserToken = null;
                return;
            }
#endif

            const int LOGON32_LOGON_INTERACTIVE = 2;
            if (loginType == 0)
                loginType = LOGON32_LOGON_INTERACTIVE;

            using (var pwBytes = creationInfo.Password.AllocGlobalUnicode())
            {
                var ret = LogonUser(username, domain, pwBytes, loginType, 0, out impersonateLoginUserToken);
                int lastErr = Marshal.GetLastWin32Error();
                var success = ret && impersonateLoginUserToken?.IsInvalid == false;
                if (!success)
                    ThrowWin32Exception("LogonUser", lastErr);
            }
        }

        #region pinvoke

        // ReSharper disable MemberCanBePrivate.Local
        // ReSharper disable InconsistentNaming
        // ReSharper disable UnusedMember.Local
        // ReSharper disable FieldCanBeMadeReadOnly.Local

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LogonUser(
            [MarshalAs(UnmanagedType.LPWStr)]string lpszUsername,
            [MarshalAs(UnmanagedType.LPWStr)]string lpszDomain,
            IntPtr passwordBytes,
            int dwLogonType,
            int dwLogonProvider,
            out SafeCloseWin32Handle phToken);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool LogonUser(
            [MarshalAs(UnmanagedType.LPWStr)]string lpszUsername,
            [MarshalAs(UnmanagedType.LPWStr)]string lpszDomain,
            [MarshalAs(UnmanagedType.LPWStr)]string password,
            int dwLogonType,
            int dwLogonProvider,
            out SafeCloseWin32Handle phToken);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcessAsUser(
            SafeHandle userToken,
            string lpApplicationName,
            string lpCommandLine,
            /*[In] ref SECURITY_ATTRIBUTES*/IntPtr lpProcessAttributes,
            /*[In] ref SECURITY_ATTRIBUTES*/IntPtr lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            ProcessCreationFlags dwCreationFlags,
            string lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFOEX lpStartupInfo,
            [Out] out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            /*[In] ref SECURITY_ATTRIBUTES*/IntPtr lpProcessAttributes,
            /*[In] ref SECURITY_ATTRIBUTES*/IntPtr lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            ProcessCreationFlags dwCreationFlags,
            string lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFOEX lpStartupInfo,
            [Out] out PROCESS_INFORMATION lpProcessInformation);

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
        internal struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
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
            public SafeHandle hStdInput;
            public SafeHandle hStdOutput;
            public SafeHandle hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool InitializeProcThreadAttributeList(byte[] lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool UpdateProcThreadAttribute(byte[] lpAttributeList, uint dwFlags, IntPtr Attribute, IntPtr[] lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void DeleteProcThreadAttributeList(byte[] lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int ResumeThread(IntPtr hThread);

        #endregion
    }
}
#endif
#endif
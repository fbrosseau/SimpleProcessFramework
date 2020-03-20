#if WINDOWS_BUILD

using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes.Windows
{
    internal partial class WindowsProcessTargetHandle : AbstractExternalProcessTargetHandle
    {
        private UnicodeEncoding s_unicodeEncoding = new UnicodeEncoding(false, false, true);

        public WindowsProcessTargetHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
            : base(info, typeResolver)
        {
        }

        protected override async Task<Process> SpawnProcess(IRemoteProcessInitializer punchHandles, CancellationToken ct)
        {
            using var builder = new CommandLineBuilder(TypeResolver, Config, ProcessCreationInfo, punchHandles.UsesStdIn, punchHandles.ExtraEnvironmentVariables);

            var cmdLine = builder.GetFullCommandLineWithExecutable();

            Logger.Debug?.Trace($"Spawning process with cmdline=[{cmdLine}]");

            //https://devblogs.microsoft.com/oldnewthing/20090601-00/?p=18083
            // Why does the CreateProcess function modify its input command line?
            var cmdLineBytes = s_unicodeEncoding.GetBytes(cmdLine);

            var envVarBuilder = new WindowsEnvironmentVariablesBlockBuilder();

            envVarBuilder.AddVariables(builder.EnvironmentVariables);

            bool isProcThreadAttributeListInitialized = false;
            AnonymousPipeServerStream remoteInputStream = null;
            UnmanagedAllocationSafeHandle procThreadAttributeListBlock = null;

            try
            {
                const int maxInheritedHandles = 5;
                var handlesToInherit = new IntPtr[maxInheritedHandles];
                using var pinnedHandlesToInherit = new DisposableGCHandle(handlesToInherit, GCHandleType.Pinned);
                var tempDuplicatedHandles = new List<SafeHandle>(maxInheritedHandles);
                var duplicatedHandlesPool = new Queue<PreExistingSafeFileHandle>(Enumerable.Range(0, maxInheritedHandles).Select(i => new PreExistingSafeFileHandle()));

                var requiredAttrListSize = IntPtr.Zero;
                if (!Win32Interop.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref requiredAttrListSize) && requiredAttrListSize == IntPtr.Zero)
                    ThrowWin32Exception("InitializeProcThreadAttributeList");
                procThreadAttributeListBlock = new UnmanagedAllocationSafeHandle(requiredAttrListSize);
                isProcThreadAttributeListInitialized = Win32Interop.InitializeProcThreadAttributeList(procThreadAttributeListBlock, 1, 0, ref requiredAttrListSize);
                if (!isProcThreadAttributeListInitialized)
                {
                    var err = Marshal.GetLastWin32Error();
                    ThrowWin32Exception("InitializeProcThreadAttributeList", err);
                }

                var startupinfo = new STARTUPINFOEX
                {
                    dwFlags = StartupInfoFlags.USESTDHANDLES | StartupInfoFlags.FORCEOFFFEEDBACK,
                    lpAttributeList = procThreadAttributeListBlock.DangerousAllocationAddress,
                };

                using var consoleRedirector = builder.ManuallyRedirectConsoleOutput
                    ? await WindowsConsoleRedirector.CreateAsync(this)
                    : null;

                SafeHandle clientInputHandle = null;

                if (punchHandles.UsesStdIn)
                {
                    remoteInputStream = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
                    clientInputHandle = remoteInputStream.ClientSafePipeHandle;
                }

                using var remoteProcessMainThreadHandle = new PreExistingSafeFileHandle();
                using var remoteProcessHandle = new PreExistingSafeFileHandle();

                if (!punchHandles.RequiresLockedEnvironmentVariables)
                {
                    envVarBuilder.AddVariables(punchHandles.ExtraEnvironmentVariables);
                    envVarBuilder.CreateFinalEnvironmentBlock();
                }
                else
                {
                    envVarBuilder.EnsureExtraCapacity();
                }

                await DoProtectedCreateProcess(punchHandles, () =>
                {
                    try
                    {
                        int validHandles = 0;

                        SafeHandle AddHandleToInherit(SafeHandle h, bool makeInheritable, HandleAccessRights? accessRights = null)
                        {
                            if (h?.IsInvalid != false)
                                return SafeHandleUtilities.NullSafeHandle;

                            if (makeInheritable)
                            {
                                var newHandle = duplicatedHandlesPool.Dequeue();
                                newHandle.DuplicateFrom(h, true, accessRights);
                                h = newHandle;
                                tempDuplicatedHandles.Add(h);
                            }

                            var ptr = h.DangerousGetHandle();
                            handlesToInherit[validHandles++] = ptr;
                            return h;
                        }

                        if (clientInputHandle != null)
                            startupinfo.hStdInput = AddHandleToInherit(clientInputHandle, true, HandleAccessRights.FileGenericRead);
                        else
                            startupinfo.hStdInput = AddHandleToInherit(Win32Interop.ConsoleHandles.DefaultStdIn, false);

                        if (builder.ManuallyRedirectConsoleOutput)
                        {
                            startupinfo.hStdOutput = AddHandleToInherit(consoleRedirector.RemoteProcessOut, true, HandleAccessRights.FileGenericWrite);
                            startupinfo.hStdError = AddHandleToInherit(consoleRedirector.RemoteProcessErr, true, HandleAccessRights.FileGenericWrite);
                        }
                        else
                        {
                            startupinfo.hStdOutput = AddHandleToInherit(Win32Interop.ConsoleHandles.DefaultStdOut, false);
                            startupinfo.hStdError = AddHandleToInherit(Win32Interop.ConsoleHandles.DefaultStdErr, false);
                        }

                        foreach (var h in punchHandles.ExtraHandlesToInherit)
                        {
                            AddHandleToInherit(h.Handle, h.MakeInheritable);
                        }

                        var totalArraySize = (IntPtr)(validHandles * IntPtr.Size);
                        const int PROC_THREAD_ATTRIBUTE_HANDLE_LIST = 0x00020002;
                        if (!Win32Interop.UpdateProcThreadAttribute(procThreadAttributeListBlock, 0, (IntPtr)PROC_THREAD_ATTRIBUTE_HANDLE_LIST, pinnedHandlesToInherit.AddrOfPinnedObject(), totalArraySize, IntPtr.Zero, IntPtr.Zero))
                            ThrowWin32Exception("UpdateProcThreadAttribute(PROC_THREAD_ATTRIBUTE_HANDLE_LIST)");

                        var creationFlags =
                            ProcessCreationFlags.EXTENDED_STARTUPINFO_PRESENT
                            | ProcessCreationFlags.CREATE_UNICODE_ENVIRONMENT
                            | ProcessCreationFlags.CREATE_SUSPENDED;

                        if (ProcessCreationInfo.TargetFramework.ProcessKind != ProcessKind.Wsl)
                            creationFlags |= ProcessCreationFlags.DETACHED_PROCESS;

                        if (punchHandles.RequiresLockedEnvironmentVariables)
                            envVarBuilder.AddVariables(punchHandles.ExtraEnvironmentVariables);

                        var environmentBlock = envVarBuilder.CreateFinalEnvironmentBlock();

                        PROCESS_INFORMATION createdProcessInfo = default;
                        try
                        {
                        }
                        finally
                        {
                            var success = Win32Interop.CreateProcess(
                                lpApplicationName: null,
                                lpCommandLine: cmdLineBytes,
                                lpProcessAttributes: IntPtr.Zero,
                                lpThreadAttributes: IntPtr.Zero,
                                bInheritHandles: true,
                                dwCreationFlags: creationFlags,
                                lpEnvironment: environmentBlock,
                                lpCurrentDirectory: builder.WorkingDirectory,
                                lpStartupInfo: startupinfo,
                                lpProcessInformation: out createdProcessInfo);

                            var err = success ? 0 : Marshal.GetLastWin32Error();
                            remoteProcessMainThreadHandle.AcquireHandle(ref createdProcessInfo.hThread);
                            remoteProcessHandle.AcquireHandle(ref createdProcessInfo.hProcess);

                            if (!success)
                                ThrowWin32Exception("CreateProcess", err);
                        }

                        Win32Interop.ResumeThread(remoteProcessMainThreadHandle);

                        return Process.GetProcessById(createdProcessInfo.dwProcessId);
                    }
                    finally
                    {
                        foreach (var tempHandle in tempDuplicatedHandles)
                        {
                            tempHandle.Dispose();
                        }
                    }
                }, ct);

                if (consoleRedirector != null)
                {
                    PrepareConsoleRedirection(ExternalProcess);
                    consoleRedirector?.StartReading();
                }

                if (punchHandles.UsesStdIn)
                {
                    var stdin = punchHandles.PayloadText;
                    Task.Run(() =>
                    {
                        var buf = Encoding.ASCII.GetBytes(stdin + "\r\n");
                        remoteInputStream.Write(buf, 0, buf.Length);
                        remoteInputStream.DisposeLocalCopyOfClientHandle();
                        remoteInputStream.Dispose();
                    }, ct).FireAndForget();
                }

                FinishProcessInitialization();
                return ExternalProcess;
            }
            finally
            {
                if (isProcThreadAttributeListInitialized)
                    Win32Interop.DeleteProcThreadAttributeList(procThreadAttributeListBlock);
            }
        }
        
        private void ThrowWin32Exception(string method, int? err = null)
        {
            throw new Win32Exception(err ?? Marshal.GetLastWin32Error());
        }
    }
}

#endif
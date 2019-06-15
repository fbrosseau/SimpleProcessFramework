using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Messages;
using Spfx.Runtime.Server.Processes.Ipc;
using Spfx.Serialization;
using Spfx.Utilities;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server.Processes
{
    internal class GenericChildProcessHandle : GenericProcessHandle, IIpcConnectorListener
    {
        private static readonly string s_windowsVersionOfWslRoot = GetDefaultRuntimeCodeBase(ProcessKind.Wsl, null);
        private static readonly Lazy<string> s_wslRootBinPath = new Lazy<string>(() => WslUtilities.GetLinuxPath(s_windowsVersionOfWslRoot), false);
        private static string EscapeArg(string a) => ProcessUtilities.EscapeArg(a);

        private Process m_targetProcess;

        public string ProcessName => ProcessCreationInfo.ProcessName;
        protected string CommandLine { get; set; }
        protected string TargetExecutable { get; set; }
        public ProcessSpawnPunchPayload RemotePunchPayload { get; private set; }

        private readonly TaskCompletionSource<string> m_processExitEvent = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        private MasterProcessIpcConnector m_ipcConnector;

        public GenericChildProcessHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
            : base(info, typeResolver)
        {
        }

        protected override async Task<ProcessInformation> CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload)
        {
            RemotePunchPayload = punchPayload;

            using (var disposeBag = new DisposeBag())
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                var ct = cts.Token;
                var remoteProcessHandles = WindowsProcessSpawnPunchHandles.Create(ProcessKind);
                disposeBag.Add(remoteProcessHandles);

                try
                {
                    var spawnTask = SpawnProcess(remoteProcessHandles, ct).WithCancellation(ct);
                    m_targetProcess = spawnTask.Result;
                }
                catch(Exception ex)
                {
                    OnProcessLost("SpawnProcess failed: " + ex.Message);
                    throw;
                }

                var connector = new MasterProcessIpcConnector(this, remoteProcessHandles, new DefaultBinarySerializer());
                disposeBag.Add(connector);

                var initTask = connector.InitializeAsync(ct).WithCancellation(ct);
                await TaskEx.ExpectFirstTask(initTask, m_processExitEvent.Task);

                m_ipcConnector = connector;
                disposeBag.ReleaseAll();

                return new ProcessInformation(ProcessUniqueId, m_targetProcess.Id, ProcessCreationInfo.ProcessKind);
            }
        }

        internal static string GetDefaultExecutableFileName(ProcessKind processKind, ProcessClusterConfiguration config)
        {
            switch (processKind)
            {
                case ProcessKind.Default:
                case ProcessKind.Netfx:
                    return config.DefaultNetfxProcessName;
                case ProcessKind.Netfx32:
                    return config.DefaultNetfx32ProcessName;
                case ProcessKind.Netcore:
                case ProcessKind.Netcore32:
                case ProcessKind.Wsl:
                    return config.DefaultNetcoreProcessName;
                default:
                    throw new ArgumentOutOfRangeException(nameof(processKind));
            }
        }

        internal static string GetDefaultRuntimeCodeBase(ProcessKind processKind, ProcessClusterConfiguration config)
        {
            if (processKind.IsNetfx() == HostFeaturesHelper.LocalProcessKind.IsNetfx())
            {
                return PathHelper.CurrentBinFolder.FullName;
            }

            string relativeCodebase;

            switch (processKind)
            {
                case ProcessKind.Netfx:
                case ProcessKind.Netfx32:
                    relativeCodebase = config?.DefaultNetfxCodeBase ?? ProcessClusterConfiguration.DefaultDefaultNetfxCodeBase;
                    break;
                case ProcessKind.Netcore:
                case ProcessKind.Netcore32:
                case ProcessKind.Wsl:
                    relativeCodebase = config?.DefaultNetcoreCodeBase ?? ProcessClusterConfiguration.DefaultDefaultNetcoreCodeBase;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(processKind));
            }

            if (Path.IsPathRooted(relativeCodebase))
                return relativeCodebase;

            return PathHelper.GetPathRelativeToBin(relativeCodebase);
        }

        private static string GetExecutableExtension(ProcessKind processKind)
        {
            switch (processKind)
            {
                case ProcessKind.Netfx:
                case ProcessKind.Netfx32:
                    return ".exe";
                default:
                    return ".dll";
            }
        }

        private string GetDefaultExecutable()
        {
            var filename = GetDefaultExecutableFileName(ProcessKind, Config);
            var ext = GetExecutableExtension(ProcessKind);

            return GetFullExecutableName(filename, ext);
        }

        private string GetFullExecutableName(string baseFilename, string ext = ".dll")
        {
            if (!baseFilename.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                baseFilename += ext;
            }

            if (ProcessKind == ProcessKind.Wsl)
            {
                return s_wslRootBinPath.Value + baseFilename;
            }

            return Path.Combine(GetDefaultRuntimeCodeBase(ProcessKind, Config), baseFilename);
        }

        protected virtual async Task<Process> SpawnProcess(IProcessSpawnPunchHandles punchHandles, CancellationToken ct)
        {
            CommandLine = $"{EscapeArg(ProcessCreationInfo.ProcessUniqueId)} {EscapeArg(Process.GetCurrentProcess().Id.ToString())}";

            ct.ThrowIfCancellationRequested();

            ComputeExecutablePath();

            if (string.IsNullOrWhiteSpace(TargetExecutable))
                TargetExecutable = GetDefaultExecutable();

            if (ProcessKind != ProcessKind.Wsl)
            {
                TargetExecutable = Path.GetFullPath(TargetExecutable);

                if (!File.Exists(TargetExecutable))
                {
                    if (!Config.CreateExecutablesIfMissing)
                        throw new InvalidProcessParametersException("The target executable does not exist");

                    CreateMissingExecutable();
                }
            }

            var commandLinePreArguments = "";
            // TODO sanitize
            if (!string.IsNullOrWhiteSpace(ProcessCreationInfo.SpecificRuntimeVersion))
            {
                var selectedVersion = HostFeaturesHelper.GetBestNetcoreRuntime(ProcessCreationInfo.SpecificRuntimeVersion, !ProcessKind.Is32Bit());
                if (string.IsNullOrWhiteSpace(selectedVersion))
                    throw new InvalidOperationException("There is no installed runtime matching " + ProcessCreationInfo.SpecificRuntimeVersion);

                commandLinePreArguments = " \"--fx-version\" " + EscapeArg(selectedVersion);
            }

            var combinedCommandLine = $"{GetExecutablePrefix()}{commandLinePreArguments} {EscapeArg(TargetExecutable)} {CommandLine}";
            combinedCommandLine = combinedCommandLine.Trim();

            var workingDir = ProcessKind == ProcessKind.Wsl ? s_windowsVersionOfWslRoot : Path.GetDirectoryName(TargetExecutable);

            var startInfo = new ProcessStartInfo(combinedCommandLine)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = ProcessCreationInfo.ManuallyRedirectConsole,
                RedirectStandardError = ProcessCreationInfo.ManuallyRedirectConsole,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            foreach(var kvp in CreateEnvironmentBlock())
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }

            string serializedPayloadForOtherProcess;

            try
            {
                RemotePunchPayload.HandshakeTimeout = 120000;

                lock (ProcessCreationUtilities.ProcessCreationLock)
                {
                    punchHandles.InitializeInLock();
                    m_targetProcess = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start process");
                    punchHandles.HandleProcessCreatedInLock(m_targetProcess, RemotePunchPayload);
                }

                if(ProcessCreationInfo.ManuallyRedirectConsole)
                {
                    var idStr = m_targetProcess.Id.ToString();
                    DataReceivedEventHandler GetLogHandler(TextWriter w)
                    {
                        return (sender, e) =>
                        {
                            if (e.Data != null)
                                w.WriteLine("{0}>{1}", idStr, e.Data);
                        };
                    }

                    m_targetProcess.OutputDataReceived += GetLogHandler(Console.Out);
                    m_targetProcess.ErrorDataReceived += GetLogHandler(Console.Error);

                    m_targetProcess.BeginErrorReadLine();
                    m_targetProcess.BeginOutputReadLine();
                }

                serializedPayloadForOtherProcess = punchHandles.FinalizeInitDataAndSerialize(m_targetProcess, RemotePunchPayload);            

                m_targetProcess.EnableRaisingEvents = true;
                m_targetProcess.Exited += (sender, args) => OnProcessLost("The process has exited with code " + ((Process)sender).ExitCode);

                if (m_targetProcess.HasExited)
                {
                    throw new InvalidOperationException("The process has exited during initialization");
                }

                ct.ThrowIfCancellationRequested();
                await m_targetProcess.StandardInput.WriteLineAsync(serializedPayloadForOtherProcess);
                ct.ThrowIfCancellationRequested();
                await m_targetProcess.StandardInput.FlushAsync();
                ct.ThrowIfCancellationRequested();
                await punchHandles.CompleteHandshakeAsync(ct);
            }
            catch(Exception ex)
            {
                OnProcessLost("The initialization failed: " + ex.Message);
                punchHandles?.DisposeAllHandles();
                m_targetProcess?.TryKill();
                throw;
            }

            return m_targetProcess;
        }

        protected virtual string GetExecutablePrefix()
        {
            switch(ProcessKind)
            {
                case ProcessKind.Netcore:
                    return EscapeArg(Path.GetFullPath(HostFeaturesHelper.GetNetCoreHostPath(true)));
                case ProcessKind.Netcore32:
                    return EscapeArg(Path.GetFullPath(HostFeaturesHelper.GetNetCoreHostPath(false)));
                case ProcessKind.Wsl:
                    return EscapeArg(WslUtilities.WslExeFullPath) + " " + EscapeArg(Config.DefaultWslNetcoreHost);
                default:
                    return "";
            }
        }

        protected virtual Dictionary<string,string> CreateEnvironmentBlock()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (DictionaryEntry currentProcessKvp in Environment.GetEnvironmentVariables())
            {
                result[currentProcessKvp.Key.ToString()] = currentProcessKvp.Value?.ToString() ?? "";
            }

            if (ProcessCreationInfo.ExtraEnvironmentVariables?.Count > 0)
            {
                foreach (var overrideKvp in ProcessCreationInfo.ExtraEnvironmentVariables)
                {
                    result[overrideKvp.Key] = overrideKvp.Value ?? "";
                }
            }

            return result;
        }

        protected void OnProcessLost(string reason, Exception ex = null)
        {
            m_processExitEvent.TrySetResult(reason);
        }

        private void CreateMissingExecutable()
        {
            var defaultExe = GetDefaultExecutable();

            try
            {
                var existingFile = new FileInfo(defaultExe);
                var existingFileSecurity = existingFile.GetAccessControl();
                existingFileSecurity.SetAccessRuleProtection(true, true);
                var copiedFile = existingFile.CopyTo(TargetExecutable, true);
                copiedFile.SetAccessControl(existingFileSecurity);
            }
            catch
            {
                TargetExecutable = defaultExe;
            }
        }

        protected virtual void ComputeExecutablePath()
        {
            var name = ProcessCreationInfo.ProcessName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetDefaultExecutableFileName(ProcessKind, Config);

            var ext = GetExecutableExtension(ProcessKind);

            TargetExecutable = GetFullExecutableName(name, ext);
        }

        protected override async Task OnTeardownAsync(CancellationToken ct)
        {
            if (m_ipcConnector != null)
                await m_ipcConnector.TeardownAsync(ct).ConfigureAwait(false);
        }

        protected override void OnDispose()
        {
            m_ipcConnector?.Dispose();
            base.OnDispose();
        }

        void IIpcConnectorListener.OnTeardownRequestReceived()
        {
            throw new NotImplementedException();
        }

        protected override void HandleMessage(string sourceConnectionId, WrappedInterprocessMessage wrappedMessage)
        {
            wrappedMessage.SourceConnectionId = sourceConnectionId;
            m_ipcConnector.ForwardMessage(wrappedMessage);
        }

        Task IIpcConnectorListener.CompleteInitialization()
        {
            return Task.CompletedTask;
        }

        void IIpcConnectorListener.OnMessageReceived(WrappedInterprocessMessage msg)
        {
            OnMessageReceivedFromProcess(msg);
        }

        void IIpcConnectorListener.OnRemoteEndLost(string msg, Exception ex = null)
        {
            OnProcessLost(msg, ex);
        }
    }
}

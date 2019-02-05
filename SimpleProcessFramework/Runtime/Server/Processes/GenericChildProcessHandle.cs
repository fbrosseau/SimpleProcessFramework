using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Spfx.Interfaces;
using Spfx.Utilities;
using System.Collections;
using Spfx.Serialization;
using Spfx.Runtime.Messages;
using Spfx.Reflection;
using System.Threading;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server.Processes
{
    internal class GenericChildProcessHandle : GenericProcessHandle, IIpcConnectorListener
    {
        private static Lazy<string> s_wslRootBinPath = new Lazy<string>(() => WslUtilities.GetWslPath(PathHelper.BinFolder.FullName), false);
        private static string EscapeArg(string a) => ProcessUtilities.EscapeArg(a);

        private Process m_targetProcess;

        public string ProcessName => ProcessCreationInfo.ProcessName;
        protected string CommandLine { get; set; }
        protected string TargetExecutable { get; set; }
        protected string WorkingDirectory { get; set; } = PathHelper.BinFolder.FullName;
        public ProcessSpawnPunchPayload RemotePunchPayload { get; private set; }

        private TaskCompletionSource<string> m_processExitEvent = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        private MasterProcessIpcConnector m_ipcConnector;

        public GenericChildProcessHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
            : base(info, typeResolver)
        {
        }

        public override async Task CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload)
        {
            RemotePunchPayload = punchPayload;

            using (var disposeBag = new DisposeBag())
            {
                var remoteProcessHandles = disposeBag.Add(await SpawnProcess());

                var connector = new MasterProcessIpcConnector(this, remoteProcessHandles, new DefaultBinarySerializer());
                disposeBag.Add(connector);

                if (!await connector.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(30)))
                    throw new TimeoutException();

                m_ipcConnector = connector;
                disposeBag.ReleaseAll();
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
            return GetFullExecutableName(GetDefaultExecutableFileName(ProcessKind, Config), GetExecutableExtension(ProcessKind));
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

            return PathHelper.GetFileRelativeToBin(baseFilename).FullName;
        }

        protected virtual async Task<IProcessSpawnPunchHandles> SpawnProcess()
        {
            CommandLine = $"{EscapeArg(ProcessCreationInfo.ProcessUniqueId)} {EscapeArg(Process.GetCurrentProcess().Id.ToString())}";

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

            //var combinedCommandLine = $"\"calc\" {GetExecutablePrefix()} {EscapeArg(TargetExecutable)} {CommandLine}";
            var combinedCommandLine = $"{GetExecutablePrefix()} {EscapeArg(TargetExecutable)} {CommandLine}";
            combinedCommandLine.Trim();

            var startInfo = new ProcessStartInfo(combinedCommandLine)
            {
                RedirectStandardInput = true,
                WorkingDirectory = WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            foreach(var kvp in CreateEnvironmentBlock())
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }

            IProcessSpawnPunchHandles punchHandles = null;
            string serializedPayloadForOtherProcess;

            try
            {
                punchHandles = ProcessKind != ProcessKind.Wsl ? (IProcessSpawnPunchHandles)new WindowsProcessSpawnPunchHandles() : new WslProcessSpawnPunchHandles();

                lock (ProcessCreationUtilities.ProcessCreationLock)
                {
                    punchHandles.InitializeInLock();
                    m_targetProcess = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start process");
                    punchHandles.HandleProcessCreatedInLock(m_targetProcess, RemotePunchPayload);
                }

                serializedPayloadForOtherProcess = punchHandles.FinalizeInitDataAndSerialize(m_targetProcess, RemotePunchPayload);            

                m_targetProcess.EnableRaisingEvents = true;
                m_targetProcess.Exited += (sender, args) => OnProcessLost("The process has exited");

                if (m_targetProcess.HasExited)
                {
                    OnProcessLost("The process has exited during initialization");
                }

                await m_targetProcess.StandardInput.WriteLineAsync(serializedPayloadForOtherProcess);
                await m_targetProcess.StandardInput.FlushAsync();
                await punchHandles.CompleteHandshakeAsync();

                return punchHandles;
            }
            catch(Exception ex)
            {
                OnProcessLost("The initialization failed: " + ex.Message);
                punchHandles?.DisposeAllHandles();
                m_targetProcess?.TryKill();
                throw;
            }
        }

        protected virtual string GetExecutablePrefix()
        {
            switch(ProcessKind)
            {
                case ProcessKind.Netcore:
                    return EscapeArg(Path.GetFullPath(Config.DefaultNetcoreHost));
                case ProcessKind.Netcore32:
                    return EscapeArg(Path.GetFullPath(Config.DefaultNetcoreHost32));
                case ProcessKind.Wsl:
                    return EscapeArg(WslUtilities.WslExeFullPath) + " " + EscapeArg(Config.DefaultWslNetcoreHost);
                case ProcessKind.Netfx:
                case ProcessKind.Netfx32:
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

        protected void OnProcessLost(string reason)
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
    }
}

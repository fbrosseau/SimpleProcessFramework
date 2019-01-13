using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SimpleProcessFramework.Interfaces;
using SimpleProcessFramework.Utilities;
using System.Collections;
using SimpleProcessFramework.Serialization;
using SimpleProcessFramework.Runtime.Messages;
using SimpleProcessFramework.Reflection;

namespace SimpleProcessFramework.Runtime.Server.Processes
{
    internal class GenericChildProcessHandle : GenericProcessHandle, IIpcConnectorListener
    {
        private Process m_targetProcess;

        public string ProcessName => ProcessCreationInfo.ProcessName;
        protected string CommandLine { get; set; }
        protected string TargetExecutable { get; set; }
        protected string WorkingDirectory { get; set; } = PathHelper.BinFolder.FullName;
        public ProcessSpawnPunchPayload RemotePunchPayload { get; private set; }

        private TaskCompletionSource<string> m_processExitEvent = new TaskCompletionSource<string>();
        private MasterProcessIpcConnector m_ipcConnector;
        private readonly IClientConnectionManager m_clientManager;

        public GenericChildProcessHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
            : base(info, typeResolver)
        {
            m_clientManager = typeResolver.GetSingleton<IClientConnectionManager>();
        }

        public override async Task CreateActualProcessAsync(ProcessSpawnPunchPayload punchPayload)
        {
            RemotePunchPayload = punchPayload;

            ComputeExecutablePath();

            if (string.IsNullOrWhiteSpace(TargetExecutable))
                TargetExecutable = GetDefaultExecutable();

            TargetExecutable = Path.GetFullPath(TargetExecutable);

            if(!File.Exists(TargetExecutable))
            {
                if (!Config.CreateExecutablesIfMissing)
                    throw new InvalidProcessParametersException("The target executable does not exist");

                CreateMissingExecutable();
            }

            using (var disposeBag = new DisposeBag())
            {
                var remoteProcessHandles = await SpawnProcess();
                disposeBag.Add(remoteProcessHandles);

                var connector = new MasterProcessIpcConnector(this, remoteProcessHandles, new DefaultBinarySerializer());
                disposeBag.Add(connector);

                if (!await connector.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(30)))
                    throw new TimeoutException();

                m_ipcConnector = connector;
                disposeBag.ReleaseAll();
            }
        }

        private static string GetDefaultExecutableFileName(ProcessKind processKind, ProcessClusterConfiguration config)
        {
            switch (processKind)
            {
                case ProcessKind.Netfx:
                    return config.DefaultNetfxProcessName;
                case ProcessKind.Netfx32:
                    return config.DefaultNetfx32ProcessName;
                case ProcessKind.Netcore:
                    return config.DefaultNetcoreProcessName;
                case ProcessKind.Netcore32:
                    return config.DefaultNetcore32ProcessName;
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

            return PathHelper.GetFileRelativeToBin(baseFilename).FullName;
        }

        protected virtual async Task<AbstractProcessSpawnPunchHandles> SpawnProcess()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = TargetExecutable,
                Arguments = CommandLine,
                RedirectStandardInput = true,
                WorkingDirectory = WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            foreach(var kvp in CreateEnvironmentBlock())
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }

            GenericProcessSpawnPunchHandles punchHandles = null;
            string serializedPayloadForOtherProcess;

            try
            {
                lock (ProcessCreationUtilities.ProcessCreationLock)
                {
                    punchHandles = new GenericProcessSpawnPunchHandles();
                    m_targetProcess = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start process");
                    serializedPayloadForOtherProcess = punchHandles.FinalizeInitDataAndSerialize(m_targetProcess, RemotePunchPayload);
                }

                m_targetProcess.EnableRaisingEvents = true;
                m_targetProcess.Exited += (sender, args) => OnProcessLost("The process has exited");

                if (m_targetProcess.HasExited)
                {
                    OnProcessLost("The process has exited during initialization");
                }

                await m_targetProcess.StandardInput.WriteLineAsync(serializedPayloadForOtherProcess);
                await m_targetProcess.StandardInput.FlushAsync();

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
            throw new NotImplementedException();
        }

        protected virtual void ComputeExecutablePath()
        {
            var name = ProcessCreationInfo.ProcessName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetDefaultExecutableFileName(ProcessKind, Config);

            var ext = GetExecutableExtension(ProcessKind);

            TargetExecutable = GetFullExecutableName(name, ext);
        }

        public override Task DestroyAsync()
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        void IIpcConnectorListener.OnMessageReceived(WrappedInterprocessMessage msg)
        {
            var origin = m_clientManager.GetClientChannel(msg.SourceConnectionId);
            if (origin is null)
                throw new InvalidOperationException("This connection no longer exists");

            origin.SendMessage(msg);
        }

        void IIpcConnectorListener.OnTeardownReceived()
        {
            throw new NotImplementedException();
        }

        protected override void HandleMessage(IInterprocessClientProxy source, WrappedInterprocessMessage wrappedMessage)
        {
            wrappedMessage.SourceConnectionId = source.UniqueId;
            m_ipcConnector.ForwardMessage(wrappedMessage);
        }

        Task IIpcConnectorListener.CompleteInitialization()
        {
            return Task.CompletedTask;
        }
    }
}

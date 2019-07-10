using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Utilities;

namespace Spfx.Runtime.Server.Processes
{
    internal class DotNetProcessTargetHandle : GenericRemoteTargetHandle
    {
        public DotNetProcessTargetHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
            : base(info, typeResolver)
        {
        }

        protected override async Task<Process> SpawnProcess(IProcessSpawnPunchHandles punchHandles, CancellationToken ct)
        {
            var startupParameters = GenericProcessStartupParameters.Create(ProcessKind);
            startupParameters.Initialize(Config, ProcessCreationInfo);

            var startInfo = new ProcessStartInfo(startupParameters.ExecutableName, startupParameters.CommandLineArguments)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = ProcessCreationInfo.ManuallyRedirectConsole,
                RedirectStandardError = ProcessCreationInfo.ManuallyRedirectConsole,
                WorkingDirectory = startupParameters.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            Logger.Debug?.Trace($"Spawning process with executable=[{startInfo.FileName}] cmdline=[{startInfo.Arguments}]");

            foreach (var kvp in startupParameters.EnvironmentBlock)
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }

            Process process = null;

            try
            {
                RemotePunchPayload.HandshakeTimeout = 120000;

                await ProcessCreationUtilities.InvokeCreateProcess(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    punchHandles.InitializeInLock();
                    process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start process");
                    punchHandles.HandleProcessCreatedInLock(process, RemotePunchPayload);
                }).ConfigureAwait(false);

                Logger.Info?.Trace("The process has been started");

                ct.ThrowIfCancellationRequested();

                if (ProcessCreationInfo.ManuallyRedirectConsole)
                {
                    var idStr = process.Id.ToString();
                    DataReceivedEventHandler GetLogHandler(TextWriter w)
                    {
                        return (sender, e) =>
                        {
                            if (e.Data != null)
                                w.WriteLine("{0}>{1}", idStr, e.Data);
                        };
                    }

                    process.OutputDataReceived += GetLogHandler(Console.Out);
                    process.ErrorDataReceived += GetLogHandler(Console.Error);

                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
                }

                var serializedPayloadForOtherProcess = punchHandles.FinalizeInitDataAndSerialize(process, RemotePunchPayload);

                process.EnableRaisingEvents = true;
                process.Exited += (sender, args) => OnProcessLost("The process has exited with code " + ((Process)sender).ExitCode);

                if (process.HasExited)
                {
                    throw new InvalidOperationException("The process has exited during initialization");
                }

                ct.ThrowIfCancellationRequested();
                await process.StandardInput.WriteLineAsync(serializedPayloadForOtherProcess);
                ct.ThrowIfCancellationRequested();
                await process.StandardInput.FlushAsync();
                ct.ThrowIfCancellationRequested();
                await punchHandles.CompleteHandshakeAsync(ct);
            }
            catch (Exception ex)
            {
                OnProcessLost("The initialization failed: " + ex.Message);
                punchHandles?.DisposeAllHandles();
                process?.TryKill();
                throw;
            }

            return process;
        }
    }
}

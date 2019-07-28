using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Utilities;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server.Processes
{
    internal class DotNetProcessTargetHandle : GenericRemoteTargetHandle
    {
        private volatile StringBuilder m_remoteProcessInitOutput = new StringBuilder();
        private AsyncManualResetEvent m_outStreamClosed = new AsyncManualResetEvent();
        private AsyncManualResetEvent m_errStreamClosed = new AsyncManualResetEvent();

        public DotNetProcessTargetHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
            : base(info, typeResolver)
        {
        }

        protected override async Task<Exception> GetInitFailureException(Exception caughtException)
        {
            var outputsClosedTask = Task.WhenAll(m_errStreamClosed.WaitAsync().AsTask(), m_outStreamClosed.WaitAsync().AsTask());
            if (await outputsClosedTask.WaitAsync(TimeSpan.FromMilliseconds(500)))
            {
                throw new ProcessInitializationException("The process initialization failed", caughtException)
                {
                    ProcessOutput = m_remoteProcessInitOutput.ToString()
                };
            }

            throw await base.GetInitFailureException(caughtException);
        }

        protected override void OnInitializationCompleted()
        {
            m_remoteProcessInitOutput = null;
        }

        protected override async Task<Process> SpawnProcess(IRemoteProcessInitializer punchHandles, CancellationToken ct)
        {
            var startupParameters = GenericProcessStartupParameters.Create(TargetFramework);
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
                    DataReceivedEventHandler GetLogHandler(bool isOut)
                    {
                        TextWriter w;
                        if (isOut || Config.PrintErrorInRegularOutput)
                            w = Console.Out;
                        else
                            w = Console.Error;

                        var name = isOut ? "Out" : "Err";
                        var fmt = process.Id + ">{0}";

                        return (sender, e) =>
                        {
                            if (e.Data is null)
                            {
                                var evt = isOut ? m_outStreamClosed : m_errStreamClosed;
                                evt.Set();
                                Logger.Debug?.Trace($"The [{name}] console has closed");
                                return;
                            }

                            var initialSb = m_remoteProcessInitOutput;
                            if (initialSb != null)
                            {
                                lock (initialSb)
                                {
                                    initialSb.AppendLine(e.Data);
                                }
                            }

                            w.WriteLine(fmt, e.Data);
                        };
                    }

                    process.OutputDataReceived += GetLogHandler(true);
                    process.ErrorDataReceived += GetLogHandler(false);

                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
                }

                var serializedPayloadForOtherProcess = punchHandles.FinalizeInitDataAndSerialize(process, RemotePunchPayload);

                process.EnableRaisingEvents = true;
                process.Exited += OnProcessExited;

                if (process.HasExited)
                {
                    HandleProcessExit(process);
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

        private void HandleProcessExit(Process process)
        {
            int exitCode = -1;
            try
            {
                exitCode = process.ExitCode;
            }
            catch
            {
            }

            OnProcessLost("The process has exited with code " + exitCode);
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            HandleProcessExit(sender as Process);
        }
    }
}

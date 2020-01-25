using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Diagnostics.Logging;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Subprocess;
using Spfx.Utilities;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server.Processes
{
    internal class ManagedProcessTargetHandle : GenericRemoteTargetHandle
    {
        private volatile StringBuilder m_remoteProcessInitOutput = new StringBuilder();
        private readonly AsyncManualResetEvent m_outStreamClosed = new AsyncManualResetEvent();
        private readonly AsyncManualResetEvent m_errStreamClosed = new AsyncManualResetEvent();

        public ManagedProcessTargetHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
            : base(info, typeResolver)
        {
        }

        protected override async Task<Exception> GetInitFailureException()
        {
            var caughtException = await base.GetInitFailureException();

            var outputsClosedTask = Task.WhenAll(m_errStreamClosed.WaitAsync().AsTask(), m_outStreamClosed.WaitAsync().AsTask());
            if (await outputsClosedTask.WaitAsync(TimeSpan.FromMilliseconds(500)))
            {
                return new ProcessInitializationException("The process initialization failed", caughtException)
                {
                    ProcessOutput = m_remoteProcessInitOutput.ToString()
                };
            }

            return caughtException;
        }

        protected override void OnInitializationCompleted()
        {
            m_remoteProcessInitOutput = null;
        }

        protected override async Task<Process> SpawnProcess(IRemoteProcessInitializer punchHandles, CancellationToken ct)
        {
            using var builder = new CommandLineBuilder(TypeResolver, Config, ProcessCreationInfo);
            Logger.Debug?.Trace($"Spawning process with executable=[{builder.PrimaryExecutableName}] cmdline=[{builder.AllFormattedArguments}]");
            ProcessStartInfo startInfo = builder.CreateProcessStartupInfo();

            Process process = null;

            try
            {
                RemotePunchPayload.HandshakeTimeout = (int)Config.CreateProcessTimeout.TotalMilliseconds;

                await ProcessCreationUtilities.InvokeCreateProcess(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    punchHandles.InitializeInLock();
                    process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start process");
                    punchHandles.HandleProcessCreatedInLock(process, RemotePunchPayload);
                }).ConfigureAwait(false);

                Logger.Info?.Trace("The process has been started");

                ct.ThrowIfCancellationRequested();

                if (builder.ManuallyRedirectConsole)
                {
                    var outputListenerFactory = TypeResolver.CreateSingleton<IStandardOutputListenerFactory>();

                    DataReceivedEventHandler CreateLogHandler(bool isOut)
                    {
                        var listener = outputListenerFactory.Create(process, isOut);
                        return (sender, e) =>
                        {
                            if (e.Data is null)
                            {
                                var name = isOut ? "Out" : "Err";
                                var evt = isOut ? m_outStreamClosed : m_errStreamClosed;
                                evt.Set();
                                Logger.Debug?.Trace($"The [{name}] console has closed");
                                listener.Dispose();
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

                            listener.OutputReceived(e.Data);
                        };
                    }

                    process.OutputDataReceived += CreateLogHandler(true);
                    process.ErrorDataReceived += CreateLogHandler(false);

                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
                }

                var serializedPayloadForOtherProcess = punchHandles.FinalizeInitDataAndSerialize(process, RemotePunchPayload);

                process.PrepareExitCode();
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
            var exitCode = process.SafeGetExitCode(defaultValue: (int)SubprocessMainShared.SubprocessExitCodes.Unknown);           
            OnProcessLost(exitCode);
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            HandleProcessExit(sender as Process);
        }
    }
}

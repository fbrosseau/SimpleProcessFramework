using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Diagnostics.Logging;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Utilities;
using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server.Processes
{
    internal interface IConsoleConsumer
    {
        void ReportConsoleOutput(StandardConsoleStream streamKind, string line);
        void ReportStreamClosed(StandardConsoleStream streamKind, Exception ex = null);
    }

    internal abstract class AbstractExternalProcessTargetHandle : GenericRemoteTargetHandle, IConsoleConsumer
    {
        private volatile StringBuilder m_remoteProcessInitOutput = new StringBuilder();
        private AsyncManualResetEvent m_outStreamClosed = new AsyncManualResetEvent();
        private AsyncManualResetEvent m_errStreamClosed = new AsyncManualResetEvent();
        private IStandardOutputListener m_outListener;
        private IStandardOutputListener m_errListener;

        protected Process ExternalProcess { get; set; }

        protected AbstractExternalProcessTargetHandle(ProcessCreationInfo info, ITypeResolver typeResolver) 
            : base(info, typeResolver)
        {
        }

        protected override void OnDispose()
        {
            if(ExternalProcess != null)
            {
                ExternalProcess.EnableRaisingEvents = false;
                ExternalProcess.Exited -= OnProcessExited;
                ExternalProcess.Dispose();
            }

            m_errStreamClosed?.Dispose();
            m_outStreamClosed?.Dispose();
            m_outListener?.Dispose();
            m_errListener?.Dispose();
            base.OnDispose();
        }

        protected override void OnInitializationCompleted()
        {
            m_remoteProcessInitOutput = null;
            m_errStreamClosed = null;
            m_outStreamClosed = null;
            base.OnInitializationCompleted();
        }

        protected override async Task<Exception> GetInitFailureException()
        {
            if (ExternalProcess != null && ExternalProcess.Id != Process.GetCurrentProcess().Id)
                ExternalProcess?.TryKill();

            var caughtException = await base.GetInitFailureException();
            if (caughtException is InvalidProcessParametersException)
                return caughtException;

            var procOutput = m_remoteProcessInitOutput;

            string finalProcessOutput = null;

            if (procOutput != null)
            {
                var outputsClosedTask = Task.WhenAll(m_errStreamClosed.WaitAsync().AsTask(), m_outStreamClosed.WaitAsync().AsTask());

                if (!await outputsClosedTask.WaitAsync(TimeSpan.FromMilliseconds(500)))
                {
                    lock (procOutput)
                    {
                        procOutput.AppendLine("*** Timeout waiting for process output to close");
                    }
                }

                lock (procOutput)
                {
                    finalProcessOutput = procOutput.ToString();
                    m_remoteProcessInitOutput = null;
                }
            }

            return new ProcessInitializationException("The process initialization failed", caughtException)
            {
                ProcessOutput = finalProcessOutput
            };
        }

        protected async Task DoProtectedCreateProcess(IRemoteProcessInitializer punchHandles, Func<Process> createProcessCallback, CancellationToken ct)
        {
            await ProcessCreationUtilities.InvokeCreateProcessAsync(() =>
            {
                ct.ThrowIfCancellationRequested();
                Logger.Debug?.Trace("Acquired global process lock");
                punchHandles.InitializeInLock();
                ExternalProcess = createProcessCallback();
                punchHandles.HandleProcessCreatedInLock(ExternalProcess);
            });

            ct.ThrowIfCancellationRequested();
            punchHandles.HandleProcessCreatedAfterLock();

            Logger.Info?.Trace("The process has been started: " + ExternalProcess.Id);
        }

        protected void FinishProcessInitialization()
        {
            void CheckHasExited()
            {
                if (ExternalProcess.HasExited)
                {
                    HandleProcessExit(ExternalProcess);
                    throw new InvalidOperationException("The process has exited during initialization");
                }
            }

            CheckHasExited();
            ExternalProcess.PrepareExitCode();
            ExternalProcess.EnableRaisingEvents = true;
            ExternalProcess.Exited += OnProcessExited;
            CheckHasExited();
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            HandleProcessExit(ExternalProcess);
        }
        
        public void ReportConsoleOutput(StandardConsoleStream stream, string data)
        {
            var listener = stream == StandardConsoleStream.Out ? m_outListener : m_errListener;
            if (data is null)
            {
                ReportStreamClosed(stream);
                return;
            }

            var initialSb = m_remoteProcessInitOutput;
            if (initialSb != null)
            {
                lock (initialSb)
                {
                    initialSb.AppendLine(data);
                }
            }

            listener.OutputReceived(data);
        }

        public void ReportStreamClosed(StandardConsoleStream stream, Exception ex = null)
        {
            var evt = stream == StandardConsoleStream.Out ? m_outStreamClosed : m_errStreamClosed;
            evt?.Set();
            Logger.Debug?.Trace($"The [{stream}] console has closed");

            var listener = stream == StandardConsoleStream.Out ? m_outListener : m_errListener;
            listener.Dispose();
        }

        protected void PrepareConsoleRedirection(Process process, object friendlyProcessId = null)
        {
            var outputListenerFactory = TypeResolver.CreateSingleton<IStandardOutputListenerFactory>();

            m_outListener = outputListenerFactory.Create(process, StandardConsoleStream.Out, friendlyProcessId);
            m_errListener = outputListenerFactory.Create(process, StandardConsoleStream.Error, friendlyProcessId);
        }

        protected void SignalStandardStreamClosed(bool isOut)
        {
            var name = isOut ? "Out" : "Err";
            var evt = isOut ? m_outStreamClosed : m_errStreamClosed;
            evt.Set();
            Logger.Debug?.Trace($"The [{name}] console has closed");
        }
    }
}

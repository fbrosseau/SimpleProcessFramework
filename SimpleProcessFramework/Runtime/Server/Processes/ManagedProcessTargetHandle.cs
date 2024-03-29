﻿using Spfx.Diagnostics.Logging;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Utilities.Threading;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes
{
    internal class ManagedProcessTargetHandle : AbstractExternalProcessTargetHandle
    {
        public ManagedProcessTargetHandle(ProcessCreationInfo info, ITypeResolver typeResolver)
            : base(info, typeResolver)
        {
        }

        protected override async Task<Process> SpawnProcess(IRemoteProcessInitializer punchHandles, CancellationToken ct)
        {
            using var builder = new CommandLineBuilder(TypeResolver, Config, ProcessCreationInfo, punchHandles.UsesStdIn, punchHandles.ExtraEnvironmentVariables);
            Logger.Debug?.Trace($"Spawning process with cmdline=[{builder.GetAllFormattedArguments()}]");
            ProcessStartInfo startInfo = builder.CreateProcessStartupInfo();

            await DoProtectedCreateProcess(punchHandles, () =>
            {
                return Process.Start(startInfo) ?? throw new InvalidOperationException("Process.Start returned null");
            }, ct).ConfigureAwait(false);

            if (builder.ManuallyRedirectConsoleOutput)
            {
                PrepareConsoleRedirection(ExternalProcess);

                ExternalProcess.OutputDataReceived += (sender, e) =>
                {
                    ReportConsoleOutput(StandardConsoleStream.Out, e.Data);
                };
                ExternalProcess.ErrorDataReceived += (sender, e) =>
                {
                    ReportConsoleOutput(StandardConsoleStream.Error, e.Data);
                };

                ExternalProcess.BeginErrorReadLine();
                ExternalProcess.BeginOutputReadLine();
            }

            if (punchHandles.UsesStdIn)
            {
                var stdin = punchHandles.PayloadText;
                Task.Run(async () =>
                {
                    await ExternalProcess.StandardInput.WriteLineAsync(stdin).ConfigureAwait(false);
                    ct.ThrowIfCancellationRequested();
                    await ExternalProcess.StandardInput.FlushAsync().ConfigureAwait(false);
                }, ct).FireAndForget();
            }

            FinishProcessInitialization();

            return ExternalProcess;
        }
    }
}

using Spfx.Runtime.Server.Processes;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities
{
    internal static class ProcessUtilities
    {
#if NET6_0_OR_GREATER
        public static int CurrentProcessId => Environment.ProcessId;
#else
        public static int CurrentProcessId { get; } = Process.GetCurrentProcess().Id;
#endif

        public static bool TryKill(this Process proc)
        {
            try
            {
                if (!proc.HasExited)
                    proc.Kill();

                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static void SetCommandLineArguments(this ProcessStartInfo info, IEnumerable<string> args)
        {
#if NETCOREAPP || NETSTANDARD2_1_PLUS
            info.ArgumentList.AddRange(args);
#else
            info.Arguments = CommandLineBuilder.FormatCommandLine(args);
#endif
        }

        internal static void SetCommandLine(this ProcessStartInfo startInfo, string executable, string cmdLine)
        {
            if (string.IsNullOrWhiteSpace(executable))
            {
                startInfo.FileName = cmdLine;
                startInfo.Arguments = "";
            }
            else
            {
                startInfo.FileName = executable;
                startInfo.Arguments = cmdLine;
            }
        }

        internal static Task<string> ExecAndGetConsoleOutput(string commandLine, TimeSpan timeout, bool trimSpacesAndNewLines = true)
        {
            return ExecAndGetConsoleOutput(null, commandLine, timeout, trimSpacesAndNewLines);
        }

        internal static async Task<string> ExecAndGetConsoleOutput(string executable, string commandLine, TimeSpan timeout, bool trimSpacesAndNewLines = true)
        {
            var procInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = PathHelper.CurrentBinFolder.FullName
            };

            procInfo.SetCommandLine(executable, commandLine);

            Process proc = null;
            await ProcessCreationUtilities.InvokeCreateProcessAsync(() =>
            {
                proc = Process.Start(procInfo);
            }).ConfigureAwait(false);

            var output = new StringBuilder();
            int nullsReceivedCount = 0;
            var completionEvent = new AsyncManualResetEvent();

            void LogHandler(object sender, DataReceivedEventArgs e)
            {
                lock (output)
                {
                    if (e.Data is null)
                    {
                        if (Interlocked.Increment(ref nullsReceivedCount) == 2)
                            completionEvent.Set();
                    }
                    else
                    {
                        output.AppendLine(e.Data);
                    }
                }
            }

            proc.OutputDataReceived += LogHandler;
            proc.ErrorDataReceived += LogHandler;

            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();

            if (!await completionEvent.WaitAsync(timeout).ConfigureAwait(false))
                throw new TimeoutException();

            if (trimSpacesAndNewLines)
                output.TrimSpacesAndNewLines();

            return output.ToString();
        }

        internal static void PrepareExitCode(this Process proc)
        {
            try
            {
                _ = proc.SafeHandle;
            }
            catch
            {
                // oh well.
            }
        }

        internal static int SafeGetExitCode(this Process proc, int defaultValue = -1)
        {
            try
            {
                return proc.ExitCode;
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
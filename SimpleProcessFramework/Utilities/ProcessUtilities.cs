using Spfx.Runtime.Server;
using Spfx.Utilities.Threading;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Spfx.Utilities
{
    internal static class ProcessUtilities
    {
        public static int CurrentProcessId { get; } = Process.GetCurrentProcess().Id;

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

        internal static Task<string> ExecAndGetConsoleOutput(string commandLine, TimeSpan timeout)
        {
            return ExecAndGetConsoleOutput(null, commandLine, timeout);
        }

        internal static async Task<string> ExecAndGetConsoleOutput(string executable, string commandLine, TimeSpan timeout)
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
            await ProcessCreationUtilities.InvokeCreateProcess(() =>
            {
                proc = Process.Start(procInfo);
            });

            var output = new StringBuilder();
            int nullsReceivedCount = 0;
            var completionEvent = new AsyncManualResetEvent();

            void LogHandler(object sender, DataReceivedEventArgs e)
            {
                lock (output)
                {
                    if (e.Data is null)
                    {
                        if (++nullsReceivedCount == 2)
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

            return output.ToString();
        }

        internal static string FormatArgument(string a)
        {
            return "\"" + a + "\"";
        }
    }
}
using Spfx.Runtime.Server;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Spfx.Utilities
{
    internal static class ProcessUtilities
    {
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

        internal static string ExecAndGetConsoleOutput(string commandLine, TimeSpan timeout)
        {
            var procInfo = new ProcessStartInfo(commandLine)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = PathHelper.CurrentBinFolder.FullName
            };

            Process proc;
            lock (ProcessCreationUtilities.ProcessCreationLock)
            {
                proc = Process.Start(procInfo);
            }

            var output = new StringBuilder();
            int nullsReceivedCount = 0;
            var completionEvent = new ManualResetEventSlim();

            void LogHandler(object sender, DataReceivedEventArgs e)
            {
                lock (output)
                {
                    if (e.Data is null)
                    {
                        if (++nullsReceivedCount == 2) completionEvent.Set();
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

            if (!proc.WaitForExit((int)timeout.TotalMilliseconds))
                throw new TimeoutException();
            if (!completionEvent.Wait(timeout))
                throw new TimeoutException();

            return output.ToString();
        }

        internal static string EscapeArg(string a)
        {
            return "\"" + a + "\"";
        }
    }
}

using Spfx.Interfaces;
using Spfx.Runtime.Server;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

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
                WorkingDirectory = PathHelper.BinFolder.FullName
            };

            Process proc;
            lock (ProcessCreationUtilities.ProcessCreationLock)
            {
                proc = Process.Start(procInfo);
            }

            var output = new StringBuilder();

            DataReceivedEventHandler handler = (sender, e) =>
            {
                lock (output)
                {
                    output.AppendLine(e.Data);
                }
            };

            proc.OutputDataReceived += handler;
            proc.ErrorDataReceived += handler;

            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();

            if (!proc.WaitForExit((int)timeout.TotalMilliseconds))
                throw new TimeoutException();

            return output.ToString();
        }

        internal static string EscapeArg(string a)
        {
            return "\"" + a + "\"";
        }
    }
}

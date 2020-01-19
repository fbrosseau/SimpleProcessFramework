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

        internal static void SetCommandLineArguments(this ProcessStartInfo info, IEnumerable<string> args)
        {
#if NETCOREAPP || NETSTANDARD2_1_PLUS
            info.ArgumentList.AddRange(args);
#else
            info.Arguments = FormatCommandLine(args);
#endif
        }

        public static string FormatCommandLine(IEnumerable<string> args)
        {
            var sb = new StringBuilder();

            foreach(var a in args)
            {
                FormatCommandLineArgument(sb, a);
                sb.Append(' ');
            }

            if (sb.Length > 0)
                --sb.Length;
            return sb.ToString();
        }

        public static string FormatCommandLineArgument(string arg)
        {
            var sb = new StringBuilder();
            FormatCommandLineArgument(sb, arg);
            return sb.ToString();
        }

        private static readonly char[] s_specialCommandLineCharacters = " \t\n\v\"\\".ToCharArray();

        public static void FormatCommandLineArgument(StringBuilder sb, string arg)
        {
            // Adapted from https://blogs.msdn.microsoft.com/twistylittlepassagesallalike/2011/04/23/everyone-quotes-command-line-arguments-the-wrong-way/

            Guard.ArgumentNotNull(arg, nameof(arg));

            if (arg.Length > 0 && arg.IndexOfAny(s_specialCommandLineCharacters) == -1)
            {
                sb.Append(arg);
                return;
            }

            sb.Append('"');
            for (int i = 0; ; ++i)
            {
                int slashes = 0;
                while (i < arg.Length && arg[i] == '\\')
                {
                    ++slashes;
                    ++i;
                }

                if (i == arg.Length)
                {
                    sb.Append('\\', slashes * 2);
                    break;
                }

                if (arg[i] == '"')
                {
                    sb.Append('\\', slashes * 2 + 1);
                }
                else
                {
                    sb.Append('\\', slashes);
                }

                sb.Append(arg[i]);
            }

            sb.Append('"');
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
            _ = proc.SafeHandle;
        }
    }
}
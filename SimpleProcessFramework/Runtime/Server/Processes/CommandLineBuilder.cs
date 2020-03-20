using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Spfx.Diagnostics.Logging;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Runtime.Server.Processes.Windows;
using Spfx.Utilities;
using Spfx.Utilities.Runtime;

namespace Spfx.Runtime.Server.Processes
{
    public class CommandLineBuilder : Disposable
    {
        private readonly ILogger m_logger;
        private readonly ProcessClusterConfiguration m_config;
        private readonly ProcessCreationInfo m_processCreationInfo;
        private bool m_needDotNetExe;
        private readonly List<string> m_dotNetExeArguments = new List<string>();
        private readonly ProcessKind m_processKind;

        internal bool ManuallyRedirectConsoleOutput { get; }
        internal bool RedirectConsoleInput { get; }
        internal string WorkingDirectory { get; private set; }
        internal Dictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>();
        internal List<string> CommandLineArguments { get; } = new List<string>();
        internal string UserExecutableName { get; private set; }
        internal string PrimaryExecutableName { get; }

        internal string GetAllFormattedArguments() => FormatCommandLine(CommandLineArguments);
        internal string GetFullCommandLineWithExecutable() => FormatCommandLine(new[] { PrimaryExecutableName }.Concat(CommandLineArguments));

        internal CommandLineBuilder(ITypeResolver typeResolver, ProcessClusterConfiguration config, ProcessCreationInfo processCreationInfo, bool redirectStdIn, IEnumerable<StringKeyValuePair> extraEnvironmentVariables = null)
        {
            m_logger = typeResolver.GetLogger(GetType(), uniqueInstance: true, friendlyName: $"{processCreationInfo.ProcessUniqueId} ({processCreationInfo.ProcessName},{processCreationInfo.TargetFramework})");

            m_config = config;
            m_processCreationInfo = processCreationInfo;
            m_processKind = m_processCreationInfo.TargetFramework.ProcessKind;

            if (!string.IsNullOrWhiteSpace(processCreationInfo.RuntimeVersionOverride))
            {
                m_dotNetExeArguments.Add(NetcoreInfo.WellKnownArguments.FrameworkVersion);
                m_dotNetExeArguments.Add(processCreationInfo.RuntimeVersionOverride);
            }

            m_needDotNetExe |= m_dotNetExeArguments.Count > 0;

            FindBestExecutableName();

            if (m_needDotNetExe)
            {
                var dotnetExe = m_processKind == ProcessKind.Wsl 
                    ? m_config.DefaultWslNetcoreHost
                    : NetcoreInfo.GetNetCoreHostPath(!processCreationInfo.TargetFramework.ProcessKind.Is32Bit());
                PrimaryExecutableName = dotnetExe;
                CommandLineArguments.AddRange(m_dotNetExeArguments);
                CommandLineArguments.Add(UserExecutableName);
            }
            else
            {
                PrimaryExecutableName = UserExecutableName;
            }

            if (m_processKind == ProcessKind.Wsl)
            {
                CommandLineArguments.Insert(0, PrimaryExecutableName); // bump the real executable to the front of the commandline, wsl.exe will take its place.
                PrimaryExecutableName = WslUtilities.WslExeFullPath;
            }

            if (config.AppendProcessIdToCommandLine)
                CommandLineArguments.Add(ProcessUtilities.CurrentProcessId.ToString());

            ManuallyRedirectConsoleOutput = m_processCreationInfo.ManuallyRedirectConsole;

            void AddEnvVars(IEnumerable<StringKeyValuePair> values)
            {
                if (values is null)
                    return;

                foreach (var kvp in values)
                {
                    EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }

            AddEnvVars(config.ExtraEnvironmentVariables);
            AddEnvVars(m_processCreationInfo.ExtraEnvironmentVariables);

            if (redirectStdIn)
            {
                RedirectConsoleInput = true;
            }

            AddEnvVars(extraEnvironmentVariables);

            if (m_processCreationInfo.ExtraCommandLineArguments?.Length > 0)
                CommandLineArguments.AddRange(m_processCreationInfo.ExtraCommandLineArguments);
        }

        protected override void OnDispose()
        {
            m_logger.Dispose();
            base.OnDispose();
        }

        private void FindBestExecutableName()
        {
            var fw = m_processCreationInfo.TargetFramework;

            var providedName = m_processCreationInfo.ProcessName;

            if (string.IsNullOrWhiteSpace(providedName))
            {
                providedName = fw.ProcessKind.IsNetfx() ? "Spfx.Process.Netfx" : "Spfx.Process.Netcore";
                if (string.IsNullOrWhiteSpace(providedName))
                    throw new InvalidProcessParametersException("No process name was provided");
            }

            // assume anything other than .dll is just part of the filename. For instance "Subprocess.Netcore" we don't want to count that as an extension.
            string GetExtension(string file)
            {
                var e = Path.GetExtension(file);
                if (".exe".Equals(e, StringComparison.OrdinalIgnoreCase) || ".dll".Equals(e, StringComparison.OrdinalIgnoreCase))
                    return e;
                return "";
            }

            var providedExtension = GetExtension(providedName);

            var providedNameWithoutExt = providedName.Substring(0, providedName.Length - providedExtension.Length);

            bool isLinux = !HostFeaturesHelper.IsWindows
                || m_processKind == ProcessKind.Wsl;

            string systemDefaultExtension = isLinux ? "" : ".exe";

            bool IsExecutable(string ext)
                => ext.Equals(systemDefaultExtension, StringComparison.OrdinalIgnoreCase);
            bool IsDll(string ext)
                => ext.Equals(".dll", StringComparison.OrdinalIgnoreCase);

            var codebase = ProcessConfig.GetCodeBase(fw, m_config);

            FileInfo GetFileInCodebase(string file)
                => new FileInfo(Path.Combine(codebase, file));

            bool TestFileExists(string file)
            {
                var fileInfo = GetFileInCodebase(file);
                if (!fileInfo.Exists)
                    return false;

                UserExecutableName = fileInfo.FullName;

                // even under WSL.exe, the working directory must be the windows version of the path.
                WorkingDirectory = Path.GetDirectoryName(UserExecutableName);

                if (m_processKind == ProcessKind.Wsl)
                {
                    UserExecutableName = WslUtilities.GetCachedLinuxPath(UserExecutableName);
                }

                var ext = GetExtension(file);

                if (IsExecutable(ext))
                    return true;

                if (IsDll(ext))
                {
                    if (m_processKind.IsNetfx())
                        throw new InvalidProcessParametersException("Cannot start a Process from a .dll file under .Net Framework");

                    m_needDotNetExe = true;
                    return true;
                }

                return false;
            }

            var filenameChoices = new List<string> { providedNameWithoutExt };
            var extensionChoices = new List<string>();

            if (!string.IsNullOrWhiteSpace(providedExtension))
            {
                extensionChoices.Add(providedExtension);
            }
            else
            {
                var netcore = m_processCreationInfo.TargetFramework as NetcoreTargetFramework;
                if (m_processKind.IsNetfx() || !(netcore?.ParsedVersion?.Major < 3))
                {
                    extensionChoices.Add(systemDefaultExtension);
                }
            }

            if (!m_processKind.IsNetfx())
                extensionChoices.Add(".dll");

            string preferredFilename = providedNameWithoutExt;
            
            if ((m_processCreationInfo.Append32BitSuffix ?? m_config.Append32BitSuffix)
                && m_processKind.Is32Bit()
                && !providedNameWithoutExt.EndsWith(m_config.SuffixFor32BitProcesses, StringComparison.OrdinalIgnoreCase))
            {
                preferredFilename = providedNameWithoutExt + m_config.SuffixFor32BitProcesses;
                filenameChoices.Insert(0, preferredFilename);
            }

            foreach(var file in filenameChoices)
            {
                foreach (var ext in extensionChoices)
                {
                    if (TestFileExists(file + ext))
                        return;
                }
            }

            if (m_config.CreateExecutablesIfMissing && !IsDll(providedExtension))
            {
                var originalExecutableName = ProcessConfig.GetDefaultExecutableName(fw, m_config);
                if (originalExecutableName != null)
                {
                    var original = GetFileInCodebase(originalExecutableName + systemDefaultExtension);
                    string tentativeCopy = preferredFilename + systemDefaultExtension;

                    try
                    {
                        var dest = GetFileInCodebase(tentativeCopy).FullName;
                        original.CopyTo(dest, false);
                        m_logger.Info?.Trace($"Successfully copied {original.FullName} to {dest}");
                    }
                    catch (Exception ex)
                    {
                        // there can be a race where someone else copied it before us or something, but as long as the file exists we're happy.
                        m_logger.Debug?.Trace(ex, "CopyFile failed");
                    }

                    if (TestFileExists(tentativeCopy))
                        return;
                }
            }

            throw new MissingSubprocessExecutableException(m_processCreationInfo.ProcessName);
        }

        internal ProcessStartInfo CreateProcessStartupInfo()
        {
            var info = new ProcessStartInfo
            {
                FileName = PrimaryExecutableName,
                RedirectStandardInput = RedirectConsoleInput,
                RedirectStandardOutput = ManuallyRedirectConsoleOutput,
                RedirectStandardError = ManuallyRedirectConsoleOutput,
                WorkingDirectory = WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            info.SetCommandLineArguments(CommandLineArguments);

            foreach (var (key, val) in EnvironmentVariables)
            {
                info.EnvironmentVariables[key] = val;
            }

            return info;
        }

        public static string[] DeconstructCommandLine(string cmdLine)
        {
            return Win32Interop.CommandLineToArgs(cmdLine);
        }

        public static string FormatCommandLine(IEnumerable<string> args)
        {
            var sb = new StringBuilder();

            foreach (var a in args)
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
    }
}

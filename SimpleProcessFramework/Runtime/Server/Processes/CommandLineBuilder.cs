﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Spfx.Diagnostics.Logging;
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Utilities;
using static Spfx.Interfaces.ProcessCreationInfo;

namespace Spfx.Runtime.Server.Processes
{
    internal class CommandLineBuilder : Disposable
    {
        private readonly ILogger m_logger;
        private readonly ProcessClusterConfiguration m_config;
        private readonly ProcessCreationInfo m_processCreationInfo;
        private bool m_needDotNetExe;
        private List<string> m_dotNetExeArguments = new List<string>();
        private ProcessKind m_processKind;

        public bool ManuallyRedirectConsole { get; private set; }
        public string WorkingDirectory { get; private set; }
        public Dictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>();
        public List<string> CommandLineArguments { get; } = new List<string>();
        public string UserExecutableName { get; private set; }
        public string PrimaryExecutableName { get; private set; }

        public string AllFormattedArguments => ProcessUtilities.FormatCommandLine(CommandLineArguments);

        public CommandLineBuilder(ITypeResolver typeResolver, ProcessClusterConfiguration config, ProcessCreationInfo processCreationInfo)
        {
            m_logger = typeResolver.GetLogger(GetType(), uniqueInstance: true, friendlyName: $"{processCreationInfo.ProcessUniqueId} ({processCreationInfo.ProcessName},{processCreationInfo.TargetFramework})");

            m_config = config;
            m_processCreationInfo = processCreationInfo;
            m_processKind = m_processCreationInfo.TargetFramework.ProcessKind;

            if (!string.IsNullOrWhiteSpace(processCreationInfo.RuntimeVersionOverride))
            {
                m_dotNetExeArguments.Add(NetcoreHelper.WellKnownArguments.FrameworkVersion);
                m_dotNetExeArguments.Add(processCreationInfo.RuntimeVersionOverride);
            }

            m_needDotNetExe |= m_dotNetExeArguments.Count > 0;

            FindBestExecutableName();

            if (m_needDotNetExe)
            {
                var dotnetExe = m_processKind == ProcessKind.Wsl 
                    ? m_config.DefaultWslNetcoreHost
                    : NetcoreHelper.GetNetCoreHostPath(!processCreationInfo.TargetFramework.ProcessKind.Is32Bit());
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

            ManuallyRedirectConsole = m_processCreationInfo.ManuallyRedirectConsole;

            void AddEnvVars(IEnumerable<StringKeyValuePair> vals)
            {
                if (vals is null)
                    return;

                foreach (var kvp in vals)
                {
                    EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }

            AddEnvVars(config.ExtraEnvironmentVariables);
            AddEnvVars(m_processCreationInfo.ExtraEnvironmentVariables);

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
                var fileinfo = GetFileInCodebase(file);
                if (!fileinfo.Exists)
                    return false;

                UserExecutableName = fileinfo.FullName;

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
                RedirectStandardInput = true,
                RedirectStandardOutput = ManuallyRedirectConsole,
                RedirectStandardError = ManuallyRedirectConsole,
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
    }
}
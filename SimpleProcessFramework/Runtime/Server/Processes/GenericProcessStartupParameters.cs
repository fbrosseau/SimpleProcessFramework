using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Spfx.Interfaces;
using Spfx.Runtime.Exceptions;
using Spfx.Utilities;

namespace Spfx.Runtime.Server.Processes
{
    internal class GenericProcessStartupParameters : IProcessStartupParameters
    {
        public string WorkingDirectory { get; private set; }
        public string ExecutableName { get; protected set; }
        public string CommandLineArguments { get; protected set; }
        public IReadOnlyDictionary<string, string> EnvironmentBlock { get; protected set; }

        protected ProcessCreationInfo ProcessCreationInfo { get; private set; }

        internal static IProcessStartupParameters Create(ProcessKind processKind)
        {
            switch(processKind)
            {
                case ProcessKind.Wsl:
                    return new WslProcessStartupParameters();
                case ProcessKind.Netcore:
                case ProcessKind.Netcore32:
                    return new NetcoreProcessStartupParameters();
                case ProcessKind.Netfx:
                case ProcessKind.Netfx32:
                    return new NetfxProcessStartupParameters();
                default:
                    throw new ArgumentException("Unexpected kind " + processKind);
            }
        }

        protected ProcessKind ProcessKind { get; private set; }
        protected ProcessClusterConfiguration Config { get; private set; }
        protected string RealExecutable { get; set; }

        public virtual void Initialize(ProcessClusterConfiguration config, ProcessCreationInfo processCreationInfo)
        {
            ProcessCreationInfo = processCreationInfo;
            ProcessKind = processCreationInfo.ProcessKind;
            Config = config;

            var name = processCreationInfo.ProcessName;
            var ext = GetExecutableExtension(ProcessKind);

            name = GetFullExecutableName(name, ext);

            if (string.IsNullOrWhiteSpace(name))
                name = GetDefaultExecutableFileName(ProcessKind, Config);

            if (!File.Exists(name))
            {
                if (!Config.CreateExecutablesIfMissing)
                    throw new InvalidProcessParametersException("The target executable does not exist");

                CreateMissingExecutable();
            }

            RealExecutable = GetFinalExecutableName(GetFullExecutableName(name, ext));

            var processArguments = new List<string>();
            processArguments.Add(RealExecutable);

            if (config.AppendProcessIdToCommandLine)
                processArguments.Add(Process.GetCurrentProcess().Id.ToString());

            if (processCreationInfo.ExtraCommandLineArguments?.Any() == true)
            {
                processArguments.AddRange(processCreationInfo.ExtraCommandLineArguments);
            }

            CreateFinalArguments(processArguments);

            SetFinalCommandLine(processArguments);

            EnvironmentBlock = CreateEnvironmentBlock();
            WorkingDirectory = GetWorkingDirectory();
        }

        protected virtual void CreateFinalArguments(List<string> processArguments)
        {
        }

        protected virtual string GetFinalExecutableName(string executableName)
        {
            return executableName;
        }

        protected virtual void SetFinalCommandLine(List<string> processArguments)
        {
            ExecutableName = processArguments[0];
            CommandLineArguments = FormatCommandLine(processArguments.Skip(1));
        }

        protected string FormatCommandLine(IEnumerable<string> args)
        {
            var sb = new StringBuilder();

            foreach (var a in args)
            {
                var formatted = ProcessUtilities.FormatArgument(a);
                if (string.IsNullOrWhiteSpace(formatted))
                    continue;
                sb.Append(formatted);
                sb.Append(' ');
            }

            if (sb.Length > 0)
                --sb.Length;

            return sb.ToString();
        }

        protected virtual string GetWorkingDirectory()
        {
            if (!string.IsNullOrWhiteSpace(RealExecutable))
                return Path.GetDirectoryName(RealExecutable);

            return PathHelper.CurrentBinFolder.FullName;
        }

        internal static string GetDefaultExecutableFileName(ProcessKind processKind, ProcessClusterConfiguration config)
        {
            switch (processKind)
            {
                case ProcessKind.Default:
                case ProcessKind.Netfx:
                    return config.DefaultNetfxProcessName;
                case ProcessKind.Netfx32:
                    return config.DefaultNetfx32ProcessName;
                case ProcessKind.Netcore:
                case ProcessKind.Netcore32:
                case ProcessKind.Wsl:
                    return config.DefaultNetcoreProcessName;
                default:
                    throw new ArgumentOutOfRangeException(nameof(processKind));
            }
        }

        private static string GetExecutableExtension(ProcessKind processKind)
        {
            switch (processKind)
            {
                case ProcessKind.Netfx:
                case ProcessKind.Netfx32:
                    return ".exe";
                default:
                    return ".dll";
            }
        }

        private string GetDefaultExecutable()
        {
            var filename = GetDefaultExecutableFileName(ProcessKind, Config);
            var ext = GetExecutableExtension(ProcessKind);

            return GetFullExecutableName(filename, ext);
        }

        private string GetFullExecutableName(string baseFilename, string ext = ".dll")
        {
            if (!baseFilename.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                baseFilename += ext;
            }

            if (Path.IsPathRooted(baseFilename))
                return baseFilename;

            return Path.Combine(HostFeaturesHelper.GetCodeBase(ProcessKind, Config), baseFilename);
        }

        private void CreateMissingExecutable()
        {
            var defaultExe = GetDefaultExecutable();

            try
            {
                var existingFile = new FileInfo(defaultExe);
                var existingFileSecurity = existingFile.GetAccessControl();
                existingFileSecurity.SetAccessRuleProtection(true, true);
                var copiedFile = existingFile.CopyTo(RealExecutable, true);
                copiedFile.SetAccessControl(existingFileSecurity);
            }
            catch
            {
                RealExecutable = defaultExe;
            }
        }

        protected virtual Dictionary<string, string> CreateEnvironmentBlock()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (DictionaryEntry currentProcessKvp in Environment.GetEnvironmentVariables())
            {
                result[currentProcessKvp.Key.ToString()] = currentProcessKvp.Value?.ToString() ?? "";
            }

            if (ProcessCreationInfo.ExtraEnvironmentVariables?.Length > 0)
            {
                foreach (var overrideKvp in ProcessCreationInfo.ExtraEnvironmentVariables)
                {
                    result[overrideKvp.Key] = overrideKvp.Value ?? "";
                }
            }

            return result;
        }
    }
}

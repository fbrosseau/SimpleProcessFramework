using System;
using System.Collections;
using System.Collections.Generic;
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

        protected TargetFramework TargetFramework { get; private set; }
        protected ProcessKind ProcessKind => TargetFramework.ProcessKind;
        protected ProcessClusterConfiguration Config { get; private set; }
        protected string RealExecutable { get; set; }

        protected ProcessCreationInfo ProcessCreationInfo { get; private set; }

        internal static IProcessStartupParameters Create(TargetFramework framework)
        {
            switch(framework.ProcessKind)
            {
                case ProcessKind.Wsl:
                    return new WslProcessStartupParameters();
                case ProcessKind.Netcore:
                case ProcessKind.Netcore32:
                    return new WindowsNetcoreProcessStartupParameters();
                case ProcessKind.Netfx:
                case ProcessKind.Netfx32:
                    return new NetfxProcessStartupParameters();
                default:
                    throw new ArgumentException("Unexpected kind " + framework);
            }
        }

        public virtual void Initialize(ProcessClusterConfiguration config, ProcessCreationInfo processCreationInfo)
        {
            ProcessCreationInfo = processCreationInfo;
            TargetFramework = processCreationInfo.TargetFramework;
            Config = config;

            var name = processCreationInfo.ProcessName;
            if (string.IsNullOrWhiteSpace(name))
                name = GetDefaultExecutableFileName(ProcessKind, Config);

            var ext = GetExecutableExtension(TargetFramework.ProcessKind);

            name = GetFullExecutableName(name, ext);

            if (!File.Exists(name))
            {
                if (!Config.CreateExecutablesIfMissing)
                    throw new MissingSubprocessExecutableException(processCreationInfo.ProcessName);

                CreateMissingExecutable(name);
            }

            RealExecutable = GetUserExecutableFullPath(GetFullExecutableName(name, ext));

            var processArguments = new List<string>();
            processArguments.Add(RealExecutable);

            if (config.AppendProcessIdToCommandLine)
                processArguments.Add(ProcessUtilities.CurrentProcessId.ToString());

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

        protected virtual string GetUserExecutableFullPath(string executableName)
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

        protected void CreateNetcoreArguments(List<string> processArguments)
        {
            if (!ProcessCreationInfo.TargetFramework.IsSupportedByCurrentProcess(Config, out var reason))
                throw new PlatformNotSupportedException(reason);

            processArguments.Insert(0, DotNetPath);

            if (ProcessCreationInfo.TargetFramework is NetcoreTargetFramework netcore
                && !string.IsNullOrWhiteSpace(netcore.TargetRuntime))
            {
                var selectedVersion = NetcoreHelper.GetBestNetcoreRuntime(netcore.TargetRuntime, ProcessKind);

                if (string.IsNullOrWhiteSpace(selectedVersion))
                    throw new InvalidOperationException("There is no installed runtime matching " + netcore.TargetRuntime);

                processArguments.Insert(1, "--fx-version");
                processArguments.Insert(2, selectedVersion);
            }
        }

        protected virtual string DotNetPath => NetcoreHelper.GetNetCoreHostPath(ProcessKind != ProcessKind.Netcore32);
        
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

            return Path.Combine(HostFeaturesHelper.GetCodeBase(TargetFramework, Config), baseFilename);
        }

        private string CreateMissingExecutable(string requestedName)
        {
            var defaultExe = GetDefaultExecutable();

            try
            {
                var existingFile = new FileInfo(defaultExe);
                var existingFileSecurity = existingFile.GetAccessControl();
                existingFileSecurity.SetAccessRuleProtection(true, true);
                var copiedFile = existingFile.CopyTo(requestedName, true);
                copiedFile.SetAccessControl(existingFileSecurity);
                return requestedName;
            }
            catch
            {
                return defaultExe;
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

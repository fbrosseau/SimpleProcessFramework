using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spfx.Subprocess
{
    internal class SubprocessMainShared
    {
        public static readonly bool VerboseLogs;
        public const string CmdLinePrefix = "--spfx-";
        public static readonly string DebugCmdLineArg = CmdLinePrefix + "debug";
        public static readonly string DescribeCmdLineArg = CmdLinePrefix + "describe";

        public static string[] GetAllKnownCommandLineArgs() => new[] { DebugCmdLineArg, DescribeCmdLineArg };

        public static readonly int BadCommandLineArgExitCode = -12345;
        public static readonly int DescribeExitCode = 0;

        static SubprocessMainShared()
        {
            var args = Environment.GetCommandLineArgs();

            if (args.Length <= 1)
            {
                Console.Error.WriteLine(
                    "This program cannot be executed directly. It is part of the SimpleProcessFramework (Spfx) version {0}.",
                    FileVersionInfo.GetVersionInfo(typeof(SubprocessMainShared).Assembly.Location).ProductVersion);

                Environment.Exit(BadCommandLineArgExitCode);
            }

            var possibleArgs = GetAllKnownCommandLineArgs();

            for (int i = 1; i < args.Length; ++i)
            {
                if (args[i] == DebugCmdLineArg)
                {
                    VerboseLogs = true;
                }
                else if (args[i].StartsWith(CmdLinePrefix) && !possibleArgs.Contains(args[i]))
                {
                    Console.Error.WriteLine("Unknown commandline parameter " + args[i]);
                    Environment.Exit(BadCommandLineArgExitCode);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void InvokeRun(Assembly asm)
        {
            if (asm is null)
                throw new FileNotFoundException("Could not load SimpleProcessFramework");
            var entryPointType = asm.GetType("Spfx.Runtime.Server.Processes.__EntryPoint", throwOnError: true);
            if (entryPointType is null)
                throw new FileNotFoundException("Could not load SimpleProcessFramework(2)");
            entryPointType.InvokeMember("Run", BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.Public, null, null, null);
        }

        public static void Initialize()
        {
            // for cctor
        }
    }
}

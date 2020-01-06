using System;
using System.Diagnostics;

namespace Spfx.Subprocess
{
    internal class SubprocessMainShared
    {
        public static readonly bool VerboseLogs;
        public static readonly string DebugCmdLineArg = "--spfxdebug";

        public static readonly int BadCommandLineArgReturnCode = -12345;

        static SubprocessMainShared()
        {
            var args = Environment.GetCommandLineArgs();

            if (args?.Length <= 1)
            {
                Console.Error.WriteLine(
                    "This program cannot be executed directly. It is part of the SimpleProcessFramework (Spfx) version {0}.",
                    FileVersionInfo.GetVersionInfo(typeof(SubprocessMainShared).Assembly.Location).ProductVersion);

                Environment.Exit(BadCommandLineArgReturnCode);
            }

            for (int i = 1; i < args.Length; ++i)
            {
                if (args[i] == DebugCmdLineArg)
                    VerboseLogs = true;
            }
        }

        public static void Initialize()
        {
            // for cctor
        }
    }
}

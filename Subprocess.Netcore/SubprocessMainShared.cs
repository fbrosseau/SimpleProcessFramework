using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spfx.Subprocess
{
    internal enum SubprocessExitCodes
    {
        Unknown = -1,
        Success = 0,
        BadCommandLine = -99999,
        InitFailure,
        HostProcessLost,
        Crash
    }

    internal class SubprocessMainShared
    {
        public static class CommandLineArgs
        {
            public const string CmdLinePrefix = "--spfx-";
            public static readonly string DebugCmdLineArg = CmdLinePrefix + "debug";
            public static readonly string DescribeCmdLineArg = CmdLinePrefix + "describe";

            public static string[] GetAllKnownCommandLineArgs() => new[] { DebugCmdLineArg, DescribeCmdLineArg };
        }

        public static class AppContextSwitches
        {
            private const string Prefix = "Switch.Spfx.";
            public const string VerboseHostLogs = Prefix + "VerboseHostLogs";
            public const string FatalExceptionCallback = Prefix + "FatalExceptionCallback";
        }

        public static Action<Exception> FatalExceptionCallback
        {
            get => (Action<Exception>)GetAppContextValue(AppContextSwitches.FatalExceptionCallback);
            set => SetAppContextValue(AppContextSwitches.FatalExceptionCallback, value);
        }

        private static object s_verboseLogs;
        private static readonly object s_false = false;
        public static bool VerboseHostLogs
        {
            get => (bool)GetAppContextValueCached(AppContextSwitches.VerboseHostLogs, ref s_verboseLogs, defaultValue: s_false);
            set => SetAppContextValueCached(AppContextSwitches.VerboseHostLogs, value, ref s_verboseLogs);
        }

        internal static void HandleFatalException(Exception ex)
        {
            FatalExceptionCallback?.Invoke(ex);

            try
            {
                Console.Error.WriteLine($"FATAL EXCEPTION -------{Environment.NewLine}{ex}");
            }
            catch
            {
                //oh well 
            }

            FinalExitProcess(SubprocessExitCodes.Crash);
        }

        internal static void FinalExitProcess(SubprocessExitCodes code)
        {
            Log($"Exiting with code {code}({(int)code})");
            Environment.Exit((int)code);
        }

        internal static bool FilterFatalException(Exception ex)
        {
            try
            {
                HandleFatalException(ex);
            }
            catch
            {
                // can't throw!
            }

            return true;
        }

        static SubprocessMainShared()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            var args = Environment.GetCommandLineArgs();

            if (args.Length <= 1)
            {
                Console.Error.WriteLine(
                    "This program cannot be executed directly. It is part of the SimpleProcessFramework (Spfx) version {0}.",
                    FileVersionInfo.GetVersionInfo(typeof(SubprocessMainShared).Assembly.Location).ProductVersion);

                FinalExitProcess(SubprocessExitCodes.BadCommandLine);
            }

            var possibleArgs = CommandLineArgs.GetAllKnownCommandLineArgs();

            for (int i = 1; i < args.Length; ++i)
            {
                if (args[i] == CommandLineArgs.DebugCmdLineArg)
                {
                    VerboseHostLogs = true;
                    AppContext.SetSwitch(AppContextSwitches.VerboseHostLogs, true);
                }
                else if (args[i].StartsWith(CommandLineArgs.CmdLinePrefix) && !possibleArgs.Contains(args[i]))
                {
                    Console.Error.WriteLine("Unknown commandline parameter " + args[i]);
                    FinalExitProcess(SubprocessExitCodes.BadCommandLine);
                }
            }

            Log("Cctor complete");
        }

        public static void Initialize()
        {
            Log(nameof(Initialize));
            // for cctor
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ExecuteRun()
        {
            Log("ExecuteRun");
            var entrypoint = GetEntryPoint();
            Log("Executing entry point");
            entrypoint();
            Log("Exited cleanly");
            FinalExitProcess(SubprocessExitCodes.Success);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Action GetEntryPoint()
        {
            Log(nameof(GetEntryPoint));
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Spfx");
            if (asm is null)
                throw new FileNotFoundException("Could not load SimpleProcessFramework");
            var entryPointType = asm.GetType("Spfx.Runtime.Server.Processes.__EntryPoint", throwOnError: true);
            if (entryPointType is null)
                throw new FileNotFoundException("Could not load SimpleProcessFramework(2)");

            var m = entryPointType.GetMethod("__Run", BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.Public);
            return (Action)Delegate.CreateDelegate(typeof(Action), m);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(string msg)
        {
            if (!VerboseHostLogs)
                return;

            Console.WriteLine(msg);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HandleFatalException(e.ExceptionObject as Exception);
        }

        internal static object GetAppContextValue(string key, object defaultValue = null)
        {
            return AppDomain.CurrentDomain.GetData(key)
                ?? defaultValue;
        }

        internal static void SetAppContextValue(string key, object value)
        {
            AppDomain.CurrentDomain.SetData(key, value);
        }

        internal static object GetAppContextValueCached(string key, ref object cache, object defaultValue = null)
        {
            if (cache is null)
                cache = GetAppContextValue(key, defaultValue);
            return cache;
        }

        internal static void SetAppContextValueCached(string key, object value, ref object cache)
        {
            cache = value;
            SetAppContextValue(key, value);
        }
    }
}

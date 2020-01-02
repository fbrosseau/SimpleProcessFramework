#if NETCOREAPP

using Spfx.Subprocess;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Spfx.Runtime.Server.Processes.HostProgram
{
    public class SpfxProgram
    {
        private static readonly string BinFolder = new FileInfo(new Uri(typeof(SpfxProgram).Assembly.Location, UriKind.Absolute).LocalPath).Directory.FullName;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Main()
        {
            SubprocessMainShared.Initialize();

            try
            {
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                AssemblyLoadContext.Default.Resolving += OnResolvingAssembly;

                var asm = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName("Spfx"));
                if (asm is null)
                    throw new FileNotFoundException("Could not load SimpleProcessFramework");
                var entryPointType = asm.GetType("Spfx.Runtime.Server.Processes.__EntryPoint");
                if (entryPointType is null)
                    throw new FileNotFoundException("Could not load SimpleProcessFramework(2)");
                entryPointType.InvokeMember("Run", BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.Public, null, null, null);
                Log("Clean exit");
            }
            catch (Exception ex)
            {
                TraceFatalException(ex);
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            TraceFatalException(e.ExceptionObject as Exception);
        }

        private static void TraceFatalException(Exception ex)
        {
            Console.Error.WriteLine($"FATAL EXCEPTION -------{Environment.NewLine}{ex}");
        }

        private static Assembly OnResolvingAssembly(AssemblyLoadContext ctx, AssemblyName name)
        {
            try
            {
                Log("Resolving " + name);
                var file = Path.Combine(BinFolder, name.Name) + ".dll";
                if (!new FileInfo(file).Exists)
                    return null;

                var a = AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
                if (a != null)
                    Log("Resolved " + name);
                return a;
            }
            catch
            {
                return null;
            }
        }

        private static void Log(string msg)
        {
            if (!SubprocessMainShared.VerboseLogs)
                return;

            Console.WriteLine(msg);
        }
    }
}
#else
namespace Spfx.Runtime.Server.Processes.HostProgram
{
    public class SpfxProgram
    {
        public static void Main(string[] args)
        {
            throw null;
        }
    }
}
#endif
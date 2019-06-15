using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;

namespace Spfx.Runtime.Server.Processes.Host
{
    internal class Program
    {
        private static readonly string BinFolder = new FileInfo(new Uri(typeof(Program).Assembly.Location, UriKind.Absolute).LocalPath).Directory.FullName;

        public static void Main(string[] args)
        {
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
                Console.WriteLine("Clean exit");
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
                Console.WriteLine("Resolving " + name);
                var file = Path.Combine(BinFolder, name.Name) + ".dll";
                if (!new FileInfo(file).Exists)
                    return null;

                var a = AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
                if (a != null)
                    Console.WriteLine("Resolved " + name);
                return a;
            }
            catch
            {
                return null;
            }
        }
    }
}
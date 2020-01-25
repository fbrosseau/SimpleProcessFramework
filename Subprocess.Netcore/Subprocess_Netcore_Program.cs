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

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // in case a custom hosts calls this, remove the noise in the callstack
        public static void Main()
        {
            try
            {
                LoadSpfx();
                SubprocessMainShared.ExecuteRun();
            }
            catch (Exception ex) when (SubprocessMainShared.FilterFatalException(ex))
            {
                SubprocessMainShared.HandleFatalException(ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LoadSpfx()
        {
            SubprocessMainShared.Initialize();
            AssemblyLoadContext.Default.Resolving += OnResolvingAssembly;
            AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName("Spfx"));
        }

        private static Assembly OnResolvingAssembly(AssemblyLoadContext ctx, AssemblyName name)
        {
            try
            {
                SubprocessMainShared.Log("Resolving " + name);
                var file = Path.Combine(BinFolder, name.Name) + ".dll";
                if (!new FileInfo(file).Exists)
                    return null;

                var a = AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
                if (a != null)
                    SubprocessMainShared.Log("Resolved " + name);
                return a;
            }
            catch
            {
                return null;
            }
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
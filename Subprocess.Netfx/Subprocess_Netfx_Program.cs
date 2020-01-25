#if !NETSTANDARD

using Spfx.Subprocess;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spfx.Runtime.Server.Processes.HostProgram
{
    public class SpfxProgram
    {
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
            Assembly.Load("Spfx");
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

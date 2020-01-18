using Spfx.Subprocess;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spfx.Runtime.Server.Processes.HostProgram
{
    public class SpfxProgram
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)] // in case a custom hosts calls this, remove the noise in the callstack
        public static void Main()
        {
            SubprocessMainShared.Initialize();
            var asm = Assembly.Load("Spfx");
            SubprocessMainShared.InvokeRun(asm);
        }
    }
}

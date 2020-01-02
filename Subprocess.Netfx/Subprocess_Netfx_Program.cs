using Spfx.Subprocess;
using System.IO;
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
            if (asm is null)
                throw new FileNotFoundException("Could not load SimpleProcessFramework");
            var entryPointType = asm.GetType("Spfx.Runtime.Server.Processes.__EntryPoint");
            if (entryPointType is null)
                throw new FileNotFoundException("Could not load SimpleProcessFramework(2)");
            entryPointType.InvokeMember("Run", BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.Public, null, null, null);
        }
    }
}

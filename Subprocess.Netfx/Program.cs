using System.IO;
using System.Reflection;

namespace Spfx.Runtime.Server.Processes.NetfxHost
{
    internal class Program
    {
        public static void Main(string[] args)
        {
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

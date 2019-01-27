using System;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Spfx
{
    class Program
    {
        static void Main(string[] args)
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

using System.IO;
using System.Reflection;

namespace SimpleProcessFramework
{
    class Program
    {
        static void Main(string[] args)
        {
            var asm = Assembly.Load("SimpleProcessFramework");
            if (asm is null)
                throw new FileNotFoundException("Could not load SimpleProcessFramework");
            var entryPointType = asm.GetType("SimpleProcessFramework.Runtime.__EntryPoint");
            entryPointType.InvokeMember("Run", BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.Public, null, null, new object[] { args });
        }
    }
}

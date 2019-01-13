using System;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace SimpleProcessFramework
{
    class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;

            var asm = Assembly.Load("SimpleProcessFramework");
            if (asm is null)
                throw new FileNotFoundException("Could not load SimpleProcessFramework");
            var entryPointType = asm.GetType("SimpleProcessFramework.Runtime.Server.Processes.__EntryPoint");
            if (entryPointType is null)
                throw new FileNotFoundException("Could not load SimpleProcessFramework(2)");
            entryPointType.InvokeMember("Run", BindingFlags.Static | BindingFlags.InvokeMethod | BindingFlags.Public, null, null, new object[] { args });
        }

        private static void OnFirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            Console.WriteLine("!!\t" + e.Exception);
        }
    }
}

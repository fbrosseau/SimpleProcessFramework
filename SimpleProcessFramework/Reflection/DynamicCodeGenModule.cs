using System.Reflection;
using System.Reflection.Emit;

namespace SimpleProcessFramework.Reflection
{
    internal static class DynamicCodeGenModule
    {
        public const string DynamicModuleName = "SimpleProcessFramework.DynamicAssembly";
        public static ModuleBuilder DynamicModule { get; }

        static DynamicCodeGenModule()
        {
            AssemblyBuilder asm = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(DynamicModuleName), AssemblyBuilderAccess.Run);
            DynamicModule = asm.DefineDynamicModule(DynamicModuleName);
        }
    }
}

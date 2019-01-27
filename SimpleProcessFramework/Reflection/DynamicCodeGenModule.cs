using System.Reflection;
using System.Reflection.Emit;

namespace Spfx.Reflection
{
    internal static class DynamicCodeGenModule
    {
        public const string DynamicModuleName = "Spfx.DynamicAssembly";
        public static ModuleBuilder DynamicModule { get; }

        static DynamicCodeGenModule()
        {
            AssemblyBuilder asm = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(DynamicModuleName), AssemblyBuilderAccess.Run);
            DynamicModule = asm.DefineDynamicModule(DynamicModuleName);
        }
    }
}

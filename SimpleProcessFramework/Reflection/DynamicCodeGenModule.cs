using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using Spfx.Properties;

namespace Spfx.Reflection
{
    internal static class DynamicCodeGenModule
    {
        public const string DynamicModuleName = "Spfx.DynamicAssembly";
        public const string DynamicModuleAssemblyIdentity = DynamicModuleName + SimpleProcessFrameworkPublicKey.Value;

        public static ModuleBuilder DynamicModule { get; }

        static DynamicCodeGenModule()
        {
            var ms = new MemoryStream();
            using (var s = typeof(DynamicCodeGenModule).Assembly.GetManifestResourceStream("Spfx.Properties.spfx.snk"))
            {
                s.CopyTo(ms);
            }

            var asmName = new AssemblyName(DynamicModuleName);
            asmName.KeyPair = new StrongNameKeyPair(ms.ToArray());
            AssemblyBuilder asm = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            DynamicModule = asm.DefineDynamicModule(DynamicModuleName);
        }
    }
}

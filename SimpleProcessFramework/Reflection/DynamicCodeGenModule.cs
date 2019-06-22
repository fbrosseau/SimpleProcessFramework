using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using Spfx.Properties;

namespace Spfx.Reflection
{
    internal static class DynamicCodeGenModule
    {
        public const string DynamicModuleName = "Spfx.DynamicAssembly";
        public const string DynamicModuleAssemblyIdentity = DynamicModuleName + SimpleProcessFrameworkPublicKey.AssemblyNameSuffix;

        public static ModuleBuilder DynamicModule { get; }

        private static readonly HashSet<Assembly> s_ignoreAccessChecks = new HashSet<Assembly>();

        static DynamicCodeGenModule()
        {
            var ms = new MemoryStream();
            using (var s = typeof(DynamicCodeGenModule).Assembly.GetManifestResourceStream("Spfx.Properties.spfx.snk"))
            {
                s.CopyTo(ms);
            }

            var asmName = new AssemblyName(DynamicModuleName);
            asmName.SetPublicKey(SimpleProcessFrameworkPublicKey.PublicKeyBytes);
            AssemblyBuilder asm = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            DynamicModule = asm.DefineDynamicModule(DynamicModuleName);
        }

        internal static void IgnoreAccessChecks(Assembly assembly)
        {
            lock (s_ignoreAccessChecks)
            {
                if (!s_ignoreAccessChecks.Add(assembly))
                    return;
            }
        }
    }
}

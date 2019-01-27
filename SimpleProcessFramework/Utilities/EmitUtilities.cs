using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Spfx.Utilities
{
    internal static class EmitUtilities
    {
        public static MethodBuilder DefineExactOverride(this TypeBuilder typeBuilder, MethodInfo methodToOverride)
        {
            var parameters = methodToOverride.GetParameters();

            MethodAttributes methodAttr = MethodAttributes.Public | MethodAttributes.Virtual;

            if (methodToOverride.IsSpecialName)
                methodAttr |= MethodAttributes.SpecialName;

            var implBuilder = typeBuilder.DefineMethod(methodToOverride.Name,
                methodAttr,
                CallingConventions.HasThis,
                methodToOverride.ReturnType,
                methodToOverride.ReturnParameter.GetRequiredCustomModifiers(),
                methodToOverride.ReturnParameter.GetOptionalCustomModifiers(),
                parameters.Select(p => p.ParameterType).ToArray(),
                parameters.Select(p => p.GetRequiredCustomModifiers()).ToArray(),
                parameters.Select(p => p.GetOptionalCustomModifiers()).ToArray());

            typeBuilder.DefineMethodOverride(implBuilder, methodToOverride);

            return implBuilder;
        }
    }
}

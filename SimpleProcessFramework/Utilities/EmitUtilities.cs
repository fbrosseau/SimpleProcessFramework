using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace SimpleProcessFramework.Utilities
{
    internal static class EmitUtilities
    {
        public static MethodBuilder DefineExactOverride(this TypeBuilder typeBuilder, MethodInfo methodToOverride)
        {
            var parameters = methodToOverride.GetParameters();

            var implBuilder = typeBuilder.DefineMethod(methodToOverride.Name,
                MethodAttributes.Public | MethodAttributes.Virtual,
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

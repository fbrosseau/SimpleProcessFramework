using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Spfx.Reflection
{
    internal static class ReflectionUtilities
    {
        public static MethodInfo FindUniqueMethod(this Type t, string name)
        {
            return t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }

        private static class TypeHelper<T>
        {
            public static readonly Type Type = typeof(T);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Type GetType<T>()
        {
            return TypeHelper<T>.Type;
        }
    }
}
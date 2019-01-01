using System;
using System.Reflection;

namespace SimpleProcessFramework.Reflection
{
    internal static class ReflectionUtilities
    {
        public static MethodInfo FindUniqueMethod(this Type t, string name)
        {
            return t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }
    }
}

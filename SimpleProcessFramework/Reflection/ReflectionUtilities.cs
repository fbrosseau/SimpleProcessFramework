using Spfx.Utilities;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spfx.Reflection
{
    internal static class ReflectionUtilities
    {
        private static readonly ThreadSafeAppendOnlyDictionary<Type, bool> m_isBlittable = new ThreadSafeAppendOnlyDictionary<Type, bool>();

        public static MethodInfo FindUniqueMethod(this Type t, string name)
        {
            return t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        }

        public static bool IsBlittable<T>()
        {
            return IsBlittableHelper<T>.Value;
        }

        private static class IsBlittableHelper<T>
        {
            public static readonly bool Value = GenericCheckIfBlittable<T>();
        }

        private static bool GenericCheckIfBlittable<T>()
        {
#if NETCOREAPP || NETSTANDARD2_1_PLUS
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                return false;
#endif
            var typeofT = typeof(T);

            // those special cases don't have a a well-defined binary format for our use-cases.
            if (typeofT == typeof(bool)
                || (typeofT.IsGenericType && typeofT.GetGenericTypeDefinition() == typeof(Nullable<>)))
                return false;

#if !NETCOREAPP && !NETSTANDARD2_1_PLUS
            try
            {
                Marshal.SizeOf(typeofT);
            }
            catch
            {
                return false;
            }
#endif
            return true;
        }

        public static bool IsBlittable(Type type)
        {
            Guard.ArgumentNotNull(type, nameof(type));

            if (type.IsInterface || type.IsClass)
                return false;

            if (m_isBlittable.TryGetValue(type, out var res))
                return res;

            res = SlowCheckIfBlittable(type);
            m_isBlittable[type] = res;
            return res;
        }

        private static bool SlowCheckIfBlittable(Type type)
        {
            if (type.IsInterface || type.IsClass)
                return false;

            var method = typeof(ReflectionUtilities).GetMethod("GenericCheckIfBlittable", BindingFlags.NonPublic | BindingFlags.Static);
            return (bool)method.MakeGenericMethod(type).Invoke(null, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsReferenceOrContainsReferences<T>()
        {
#if NETCOREAPP3_0_PLUS
            return RuntimeHelpers.IsReferenceOrContainsReferences<T>();
#else
            return IsBlittable<T>();
#endif
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
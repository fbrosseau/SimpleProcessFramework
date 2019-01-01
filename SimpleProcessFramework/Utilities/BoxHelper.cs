using Oopi.Utilities;
using SimpleProcessFramework.Reflection;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace SimpleProcessFramework.Utilities
{
    internal static class BoxHelper
    {
        public static readonly object BoxedCancellationToken = CancellationToken.None;
        private static readonly object[] s_boxedInts = Enumerable.Range(-10, 100).Select(i => (object)i).ToArray();

        public static object Box<T>(T val)
        {
            if (typeof(T) == typeof(VoidType))
            {
                return VoidType.BoxedValue;
            }

            if (typeof(T) == typeof(int))
            {

            }

            return val;
        }

        internal static class Reflection
        {
            public static MethodInfo GetBoxMethod(Type t) => typeof(BoxHelper).FindUniqueMethod(nameof(Box)).MakeGenericMethod(t);
            public static FieldInfo BoxedCancellationTokenField => typeof(BoxHelper).GetField(nameof(BoxedCancellationToken), BindingFlags.Public | BindingFlags.Static);
        }
    }
}
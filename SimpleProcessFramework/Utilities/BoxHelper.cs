using Oopi.Utilities;
using SimpleProcessFramework.Reflection;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SimpleProcessFramework.Utilities
{
    internal static class BoxHelper
    {
        public static readonly object BoxedCancellationToken = CancellationToken.None;

        private abstract class BoxHelperImpl<T>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public abstract object Box(T val);
        }

        private sealed class Int32BoxHelper : BoxHelperImpl<int>
        {
            private const int s_min = -10;
            private const int s_max = 100;
            private static readonly object[] s_boxedInts = Enumerable.Range(s_min, s_max - s_min).Select(i => (object)i).ToArray();

            public static readonly Int32BoxHelper Instance = new Int32BoxHelper();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override object Box(int val)
            {
                if (val >= s_min && val <= s_max)
                    return s_boxedInts[val - s_min];

                return val;
            }
        }

        private sealed class Int64BoxHelper : BoxHelperImpl<long>
        {
            private const int s_min = -10;
            private const int s_max = 100;
            private static readonly object[] s_boxedInts = Enumerable.Range(s_min, s_max - s_min).Select(i => (object)(long)i).ToArray();

            public static readonly Int64BoxHelper Instance = new Int64BoxHelper();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override object Box(long val)
            {
                if (val >= s_min && val <= s_max)
                    return s_boxedInts[val - s_min];

                return val;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object Box<T>(T val)
        {
            if (typeof(T) == typeof(VoidType))
                return VoidType.BoxedValue;
            if (typeof(T) == typeof(int))
                return ((BoxHelperImpl<T>)(object)Int32BoxHelper.Instance).Box(val);
            if (typeof(T) == typeof(long))
                return ((BoxHelperImpl<T>)(object)Int64BoxHelper.Instance).Box(val);

            return val;
        }

        internal static class Reflection
        {
            public static MethodInfo GetBoxMethod(Type t) => typeof(BoxHelper).FindUniqueMethod(nameof(Box)).MakeGenericMethod(t);
            public static FieldInfo BoxedCancellationTokenField => typeof(BoxHelper).GetField(nameof(BoxedCancellationToken), BindingFlags.Public | BindingFlags.Static);
        }
    }
}
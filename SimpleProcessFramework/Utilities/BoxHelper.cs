using Oopi.Utilities;
using SimpleProcessFramework.Reflection;
using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object Box<T>(T val)
        {
            if (typeof(T) == typeof(VoidType))
                return VoidType.BoxedValue;
            if (typeof(T) == typeof(int))
                return ((BoxHelperImpl<T>)(object)Int32BoxHelper.Instance).Box(val);
            if (typeof(T) == typeof(long))
                return ((BoxHelperImpl<T>)(object)Int64BoxHelper.Instance).Box(val);
            if (typeof(T) == typeof(DateTime))
                return ((BoxHelperImpl<T>)(object)DateTimeBoxHelper.Instance).Box(val);
            if (typeof(T) == typeof(TimeSpan))
                return ((BoxHelperImpl<T>)(object)TimeSpanBoxHelper.Instance).Box(val);
            return val;
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

        private sealed class DateTimeBoxHelper : BoxHelperImpl<DateTime>
        {
            private static readonly object s_minValue = DateTime.MinValue;
            private static readonly object s_maxValue = DateTime.MaxValue;

            public static readonly DateTimeBoxHelper Instance = new DateTimeBoxHelper();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override object Box(DateTime val)
            {
                if (val == DateTime.MinValue)
                    return s_minValue;
                if (val == DateTime.MaxValue)
                    return s_maxValue;
                return val;
            }
        }

        private sealed class TimeSpanBoxHelper : BoxHelperImpl<TimeSpan>
        {
            private static readonly object s_minValue = TimeSpan.MinValue;
            private static readonly object s_maxValue = TimeSpan.MaxValue;
            private static readonly object s_zero = TimeSpan.Zero;

            public static readonly TimeSpanBoxHelper Instance = new TimeSpanBoxHelper();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override object Box(TimeSpan val)
            {
                if (val == TimeSpan.Zero)
                    return s_zero;
                if (val == TimeSpan.MinValue)
                    return s_minValue;
                if (val == TimeSpan.MaxValue)
                    return s_maxValue;
                return val;
            }
        }

        internal static class Reflection
        {
            public static MethodInfo GetBoxMethod(Type t) => typeof(BoxHelper).FindUniqueMethod(nameof(Box)).MakeGenericMethod(t);
            public static FieldInfo BoxedCancellationTokenField => typeof(BoxHelper).GetField(nameof(BoxedCancellationToken), BindingFlags.Public | BindingFlags.Static);

            internal static void EmitBox(ILGenerator ilgen, Type argType)
            {
                ilgen.EmitCall(OpCodes.Call, GetBoxMethod(argType), null);
            }
        }
    }
}
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace System.Text
{
    internal static class StringBuilderGlue
    {
#if !NETCOREAPP && !NETSTANDARD2_1_PLUS
        public static void AppendJoin<T>(this StringBuilder sb, char separator, params T[] values)
        {
            AppendJoin(sb, separator, (IEnumerable<T>)values);
        }
        public static void AppendJoin<T>(this StringBuilder sb, char separator, IEnumerable<T> values)
        {
            bool first = true;
            foreach (var v in values)
            {
                if (!first)
                    sb.Append(separator);
                else
                    first = false;

                sb.Append(v);
            }
        }
#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AppendJoin<T>(StringBuilder sb, char separator, params T[] values) => sb.AppendJoin(separator, values);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AppendJoin<T>(StringBuilder sb, char separator, IEnumerable<T> values) => sb.AppendJoin(separator, values);
#endif
    }
}
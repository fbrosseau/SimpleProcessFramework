using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Spfx.Utilities
{
    internal static class MostRandomUtilities
    {
        private static readonly HashSet<Type> s_trivialDisplayTypes = new HashSet<Type>
        {
            // NOT including string as that may be huge and we don't want that in TinySummaryString.
            typeof(sbyte),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(byte),
            typeof(ushort),
            typeof(uint),
            typeof(ulong),
            typeof(IntPtr),
            typeof(UIntPtr),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(Guid)
        };

        internal static string FormatObjectToTinyString(object result, int targetMaxDisplayLength = 32)
        {
            if (result is null)
                return "<null>";
            var t = result.GetType();
            if (s_trivialDisplayTypes.Contains(t) || t.IsEnum)
                return result.ToString();

            if (t == typeof(string))
            {
                var str = result.ToString();

                var crop = targetMaxDisplayLength - 6;

                if (str.Length > targetMaxDisplayLength)
                    return "\"" + str.Substring(0, crop) + "...\"+" + (str.Length - crop);
                else
                    return "\"" + str + "\"";
            }

            if (t.IsArray)
                return "<" + ((ICollection)result).Count + " " + t.Name + ">";
            return "<" + t.Name + ">";
        }
    }
}

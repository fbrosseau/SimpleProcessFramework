using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
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
            typeof(Guid),
            typeof(Version),
            typeof(IPAddress),
            typeof(EndPoint)
        };

        internal static string FormatObjectToTinyString(object result, int suggestedMaxCharacters = 32)
        {
            if (result is null)
                return "<null>";
            var t = result.GetType();
            if (s_trivialDisplayTypes.Contains(t) || t.IsEnum)
                return result.ToString();

            if (t == typeof(string))
            {
                // I know this can exceed the max by a few characters but I didn't bother to make this exact :)
                var str = result.ToString();

                var crop = suggestedMaxCharacters - 6;

                if (str.Length > suggestedMaxCharacters)
                    return "\"" + str.Substring(0, crop) + "...\"+" + (str.Length - crop);
                else
                    return "\"" + str + "\"";
            }

            if (t.IsArray)
                return "<" + ((ICollection)result).Count + " " + t.Name + ">";

            var baseT = t.BaseType;
            while (baseT != null && baseT != typeof(object))
            {
                if (s_trivialDisplayTypes.Contains(t))
                    return result.ToString();
                baseT = baseT.BaseType;
            }

            return "<" + t.Name + ">";
        }
    }
}
using Spfx.Interfaces;
using Spfx.Reflection;
using Spfx.Utilities.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

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

        private static Regex s_netfxExceptionTidyRegex;
        private static MatchEvaluator s_netfxExceptionTidyRegexMatchEvaluator;

        internal static object ExceptionToTidyString(Exception ex)
        {
            if (ex is null)
                return null;

            var original = ex.ToString();
            if (!HostFeaturesHelper.LocalProcessKind.IsNetfx())
                return original;

            if(s_netfxExceptionTidyRegex is null)
            {
                s_netfxExceptionTidyRegexMatchEvaluator = m =>
                {
                    var g = m.Groups["asyncMethod"];
                    if (!g.Success)
                        return "";
                    return g.Value + "(...)";
                };

                s_netfxExceptionTidyRegex = new Regex(@"
(
    \s*---.*?---\s*\n
    .*?ExceptionDispatchInfo\.Throw\(\)\s*\n
    (.*?HandleNonSuccessAndDebuggerNotification.*\n)?
) | (
    \.<(?<asyncMethod>.*?)>d__\d+\.MoveNext\(\)
)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);
            }

            return s_netfxExceptionTidyRegex.Replace(original, s_netfxExceptionTidyRegexMatchEvaluator);
        }

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

        internal static bool ArraysEqual<T>(T[] a, T[] b)
        {
            if (a is null)
                return b is null;
            if (b is null)
                return false;
            if (a.Length != b.Length)
                return false;

            for(int i = 0; i < a.Length; ++i)
            {
                if (!EqualityComparer<T>.Default.Equals(a[i], b[i]))
                    return false;
            }

            return true;
        }

        internal static int HashArray<T>(T[] values)
        {
            if (values is null)
                return -1;

            int hash = 0;
            for(int i = 0; i < values.Length;++i)
            {
                hash ^= EqualityComparer<T>.Default.GetHashCode(values[i]) + i;
            }
            return hash;
        }
    }
}
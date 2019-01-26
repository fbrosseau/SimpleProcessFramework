using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SimpleProcessFramework.Utilities
{
    internal class Guard
    {
        [DebuggerStepThrough, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ArgumentNotNull(object argValue, string argName)
        {
            if (argValue is null)
            {
                Debug.Fail("Argument null! " + argName);
                throw new ArgumentNullException(argName);
            }
        }

        internal static void ArgumentNotNullOrEmpty(string argValue, string argName)
        {
            if (argValue is null)
                throw new ArgumentNullException(argName);
            if (argValue.Length == 0)
                throw new ArgumentException("Argument cannot be an empty string", argName);
        }
    }
}

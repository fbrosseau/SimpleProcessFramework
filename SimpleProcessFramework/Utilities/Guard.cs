using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Oopi.Utilities
{
    internal class Guard
    {
        [DebuggerStepThrough, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ArgumentNotNull(object argValue, string argName)
        {
            if (argValue is null)
                throw new ArgumentNullException(argName);
        }

        [DebuggerStepThrough, DebuggerHidden, MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ArgumentNotNullAndAssert(object argValue, string argName)
        {
            Debug.Assert(argValue != null);
            ArgumentNotNull(argValue, argName);
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

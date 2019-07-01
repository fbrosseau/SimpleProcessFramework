using System;
using System.ComponentModel;

namespace Spfx.Utilities.Diagnostics
{
    [Flags]
    [EditorBrowsable(EditorBrowsableState.Never)] // marking as Browsable-Never to avoid polluting people's intellisense with very common names
    public enum LogTraceLevel
    {
        Debug = 1,
        Info = 2,
        Warn = 4,
        Error = 8,

        All = Debug | Info | Warn | Error
    }
}

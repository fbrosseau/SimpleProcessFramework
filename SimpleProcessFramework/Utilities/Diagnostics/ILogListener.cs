using System;
using System.ComponentModel;

namespace Spfx.Utilities.Diagnostics
{
    [EditorBrowsable(EditorBrowsableState.Never)] // marking as Browsable-Never to avoid polluting people's intellisense with very common names
    public interface ILogListener
    {
        void Log(string name, LogTraceLevel level, string message, Exception ex = null);
    }
}

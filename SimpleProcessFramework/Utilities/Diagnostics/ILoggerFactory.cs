using System;
using System.ComponentModel;

namespace Spfx.Utilities.Diagnostics
{
    [EditorBrowsable(EditorBrowsableState.Never)] // marking as Browsable-Never to avoid polluting people's intellisense with very common names
    public interface ILoggerFactory
    {
        ILogger GetLogger(Type loggedType, bool uniqueInstance = false);
    }
}

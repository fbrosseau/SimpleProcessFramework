using System;
using System.ComponentModel;

namespace SimpleProcessFramework.Utilities
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface ILogger
    {
        void TraceInfo(string msg);
        void TraceWarning(string msg);
        void TraceError(string msg);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface ILoggerFactory
    {
        ILogger GetLogger(Type loggedType);
    }
}

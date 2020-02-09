using Spfx.Runtime.Server.Processes;
using System;
using System.Diagnostics;

namespace Spfx.Diagnostics.Logging
{
    public enum StandardConsoleStream
    {
        Out,
        Error
    }

    public interface IStandardOutputListenerFactory
    {
        IStandardOutputListener Create(Process process, StandardConsoleStream stream, object friendlyProcessId = null);
    }
}

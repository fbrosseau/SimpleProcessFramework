using System.IO;

namespace Spfx.Diagnostics.Logging
{
    public interface IConsoleProvider
    {
        TextWriter Out { get; }
        TextWriter Err { get; }
    }
}

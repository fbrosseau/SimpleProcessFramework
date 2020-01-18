using System.Diagnostics;

namespace Spfx.Diagnostics.Logging
{
    public interface IStandardOutputListenerFactory
    {
        IStandardOutputListener Create(Process process, bool standardOut);
    }
}

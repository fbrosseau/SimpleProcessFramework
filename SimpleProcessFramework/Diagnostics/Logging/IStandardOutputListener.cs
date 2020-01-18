using System;

namespace Spfx.Diagnostics.Logging
{
    public interface IStandardOutputListener : IDisposable
    {
        void OutputReceived(string data);
    }
}

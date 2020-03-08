using System;

namespace Spfx
{
    internal static class ProcessEndpointAddressCache
    {
        internal static bool TryGetCachedValue(string addr, out ProcessEndpointAddress ep)
        {
            ep = null;
            return false;
        }

        internal static IDisposable RegisterWellKnownAddress(ProcessEndpointAddress uniqueAddress)
        {
            return null;
        }
    }
}

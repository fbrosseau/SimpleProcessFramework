using Spfx.Utilities.Threading;
using System;
using System.Threading.Tasks;

namespace Spfx.Utilities
{
    public static class ProcessCreationUtilities
    {
        public static readonly object ProcessCreationLock = new object();
        private static readonly AsyncLock s_asyncLock = new AsyncLock();

        public static async ValueTask InvokeCreateProcessAsync(Action createProcessCallback)
        {
            using var lockSession = await s_asyncLock.LockAsync().ConfigureAwait(false);

            lock (ProcessCreationLock)
            {
                createProcessCallback();
            }
        }
    }
}

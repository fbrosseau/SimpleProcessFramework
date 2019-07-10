using Spfx.Utilities.Threading;
using System;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server
{
    public static class ProcessCreationUtilities
    {
        public static readonly object ProcessCreationLock = new object();
        private static readonly AsyncLock s_asyncLock = new AsyncLock();

        public static async Task InvokeCreateProcess(Action createProcessCallback)
        {
            using (await s_asyncLock.LockAsync().ConfigureAwait(false))
            {
                lock (ProcessCreationLock)
                {
                    createProcessCallback();
                }
            }
        }
    }
}

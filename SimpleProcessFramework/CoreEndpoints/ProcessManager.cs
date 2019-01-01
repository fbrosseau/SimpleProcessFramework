using SimpleProcessFramework.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleProcessFramework.CoreEndpoints
{
    internal class ProcessManager : IProcessManager
    {
        public Task AutoDestroy()
        {
            return Task.FromException(new Exception());
        }

        public Task AutoDestroy2(int i, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<int>();
            ct.Register(() => tcs.TrySetResult(5));
            return tcs.Task;
        }

        public Task AutoDestroy3()
        {
            throw new NotImplementedException();
        }
    }
}

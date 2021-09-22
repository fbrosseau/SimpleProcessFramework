using Spfx.Interfaces;
using Spfx.Utilities;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes
{
    public class LazyProcess
    {
        public LazyProcess(SafeHandle processHandle)
        {

        }

        public LazyProcess(int pid)
        {

        }

        public LazyProcess(Process proc)
        {

        }

        public SafeHandle ProcessHandle { get; internal set; }
    }

    internal abstract class AbstractProcessInitializer : Disposable, IRemoteProcessInitializer
    {
        public abstract bool UsesStdIn { get; }

        public string PayloadText { get; protected set; }

        protected ProcessSpawnPunchPayload InitData { get; private set; }
        protected CancellationToken CancellationToken { get; private set; }

        public virtual bool RequiresLockedEnvironmentVariables => false;
        public virtual IEnumerable<StringKeyValuePair> ExtraEnvironmentVariables
            => Enumerable.Empty<StringKeyValuePair>();
        public virtual IEnumerable<SafeHandleToInherit> ExtraHandlesToInherit
            => Enumerable.Empty<SafeHandleToInherit>();

        protected Process Process { get; private set; }

        public virtual ValueTask InitializeAsync(ProcessSpawnPunchPayload initData, CancellationToken ct)
        {
            InitData = initData;
            CancellationToken = ct;
            return default;
        }

        public virtual void InitializeInLock()
        {
        }

        public virtual void DisposeAllHandles()
        {
        }

        public virtual void HandleProcessCreatedInLock(Process proc)
        {
            Process = proc;
        }

        public virtual void HandleProcessCreatedAfterLock()
        {
            PayloadText = InitData.SerializeToString();
        }

        public virtual ValueTask CompleteHandshakeAsync()
            => default;

        public abstract (Stream readStream, Stream writeStream) AcquireIOStreams();
    }
}
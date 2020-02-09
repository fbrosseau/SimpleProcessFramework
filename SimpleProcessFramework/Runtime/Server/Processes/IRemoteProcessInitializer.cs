using Spfx.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes
{
    internal interface IProcessInitializer : IDisposable
    {
    }

    internal readonly struct SafeHandleToInherit
    {
        public SafeHandle Handle { get; }
        public bool MakeInheritable { get; }

        public SafeHandleToInherit(SafeHandle h, bool makeInheritable)
        {
            Handle = h;
            MakeInheritable = makeInheritable;
        }
    }

    internal interface IRemoteProcessInitializer : IProcessInitializer
    {
        bool UsesStdIn { get; }
        string PayloadText { get; }

        IEnumerable<SafeHandleToInherit> ExtraHandlesToInherit { get; }

        IEnumerable<StringKeyValuePair> ExtraEnvironmentVariables { get; }
        bool RequiresLockedEnvironmentVariables { get; }

        ValueTask InitializeAsync(ProcessSpawnPunchPayload initData, CancellationToken ct);
        void DisposeAllHandles();
        void InitializeInLock();

        void HandleProcessCreatedInLock(Process proc);
        void HandleProcessCreatedAfterLock();

        ValueTask CompleteHandshakeAsync();
        (Stream readStream, Stream writeStream) AcquireIOStreams();
    }
}
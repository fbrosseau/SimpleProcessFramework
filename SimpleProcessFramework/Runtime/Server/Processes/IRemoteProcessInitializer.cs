using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes
{
    internal interface IProcessInitializer : IDisposable
    {
    }

    internal interface IRemoteProcessInitializer : IProcessInitializer
    {
        Stream ReadStream { get; }
        Stream WriteStream { get; }

        ValueTask InitializeAsync(CancellationToken ct);
        string FinalizeInitDataAndSerialize(Process targetProcess, ProcessSpawnPunchPayload remotePunchPayload);
        void DisposeAllHandles();
        void InitializeInLock();
        void HandleProcessCreatedInLock(Process targetProcess, ProcessSpawnPunchPayload initData);
        ValueTask CompleteHandshakeAsync(CancellationToken ct);
    }
}
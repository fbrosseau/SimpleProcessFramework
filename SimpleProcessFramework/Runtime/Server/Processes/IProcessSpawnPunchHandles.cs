using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes
{
    internal interface IProcessSpawnPunchHandles : IDisposable
    {
        Stream ReadStream { get; }
        Stream WriteStream { get; }

        string FinalizeInitDataAndSerialize(Process targetProcess, ProcessSpawnPunchPayload remotePunchPayload);
        void DisposeAllHandles();
        void InitializeInLock();
        void HandleProcessCreatedInLock(Process targetProcess, ProcessSpawnPunchPayload initData);
        Task CompleteHandshakeAsync(CancellationToken ct);
    }
}
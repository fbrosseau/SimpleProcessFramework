using Spfx.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;

namespace Spfx.Runtime.Server.Processes
{
    internal abstract class AnonymousPipeProcessSpawnPunchHandles : AbstractProcessInitializer
    {
        private bool m_disposeClientHandles;

        public AnonymousPipeServerStream ReadPipe { get; protected set; }
        public AnonymousPipeServerStream WritePipe { get; protected set; }

        public override bool UsesStdIn => true;

        protected override void OnDispose()
        {
            base.OnDispose();
            DisposeAllHandles();
        }

        public override void DisposeAllHandles()
        {
            base.DisposeAllHandles();
            ReleaseRemoteProcessHandles();
            ReadPipe?.Dispose();
            WritePipe?.Dispose();
        }

        internal virtual void ReleaseRemoteProcessHandles()
        {
            DisposeClientHandle(ReadPipe);
            DisposeClientHandle(WritePipe);
        }

        public override void HandleProcessCreatedInLock(Process proc)
        {
            base.HandleProcessCreatedInLock(proc);

            m_disposeClientHandles = proc.Id != ProcessUtilities.CurrentProcessId;

            // those are inverted on purpose.
            InitData.WritePipe = CreateHandleForOtherProcess(ReadPipe);
            InitData.ReadPipe = CreateHandleForOtherProcess(WritePipe);
            InitData.ShutdownEvent = SafeHandleUtilities.SerializeHandle(GetShutdownHandleForOtherProcess());
        }

        protected abstract IntPtr GetShutdownHandleForOtherProcess();

        private string CreateHandleForOtherProcess(AnonymousPipeServerStream stream)
        {
            string handle = stream.GetClientHandleAsString();
            DisposeClientHandle(stream);
            return handle;
        }

        private void DisposeClientHandle(AnonymousPipeServerStream stream)
        {
            if (m_disposeClientHandles)
                stream?.DisposeLocalCopyOfClientHandle();
        }

        public override (Stream readStream, Stream writeStream) AcquireIOStreams()
        {
            var r = ReadPipe;
            ReadPipe = null;
            var w = WritePipe;
            WritePipe = null;
            return (r, w);
        }
    }
}
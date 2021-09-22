using Microsoft.Win32.SafeHandles;
using Spfx.Io;
using Spfx.Reflection;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Collections.Generic;
using System.IO.Pipes;

namespace Spfx.Runtime.Server.Processes.Hosting
{
    internal class WindowsProcessContainerInitializer : RemoteProcessContainerInitializer
    {
        private readonly NamedPipeClientStream m_pipe;

        public WindowsProcessContainerInitializer(ProcessSpawnPunchPayload payload, ITypeResolver typeResolver)
            : base(payload, typeResolver)
        {
            m_pipe = new NamedPipeClientStream(PipeDirection.InOut, true, true, new SafePipeHandle(SafeHandleUtilities.DeserializeRawHandleFromString(payload.WritePipe), true));
        }

        internal override ILengthPrefixedStreamReader CreateReader()
        {
            return new AsyncLengthPrefixedStreamReader(m_pipe);
        }

        internal override ILengthPrefixedStreamWriter CreateWriter()
        {
            return new AsyncLengthPrefixedStreamWriter(m_pipe);
        }

        internal override IEnumerable<SubprocessShutdownEvent> GetHostShutdownEvents()
        {
            if (string.IsNullOrWhiteSpace(Payload.ShutdownEvent))
                throw new InvalidOperationException("Expected a valid ShutdownEvent");

            var h = SafeHandleUtilities.CreateWaitHandleFromString(Payload.ShutdownEvent);
            return new[] { new SubprocessShutdownEvent(h.WaitAsync(), "Host shutdown") };
        }
    }
}
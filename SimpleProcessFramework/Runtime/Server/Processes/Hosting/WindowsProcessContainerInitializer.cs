using Spfx.Utilities.Threading;
using System.Collections.Generic;
using Spfx.Reflection;
using Spfx.Io;
using System.IO.Pipes;
using Microsoft.Win32.SafeHandles;
using System;
using Spfx.Utilities;

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
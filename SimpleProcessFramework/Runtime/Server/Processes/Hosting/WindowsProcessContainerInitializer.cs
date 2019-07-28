using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Spfx.Reflection;
using Spfx.Io;
using System.IO.Pipes;

namespace Spfx.Runtime.Server.Processes.Hosting
{
    internal class WindowsProcessContainerInitializer : RemoteProcessContainerInitializer
    {
        public WindowsProcessContainerInitializer(ProcessSpawnPunchPayload payload, ITypeResolver typeResolver)
            : base(payload, typeResolver)
        {
        }

        internal override ILengthPrefixedStreamReader CreateReader()
        {
            var readStream = new AnonymousPipeClientStream(PipeDirection.In, Payload.ReadPipe);
            return new SyncLengthPrefixedStreamReader(readStream, Payload.ProcessUniqueId + " - SubprocessRead");
        }

        internal override ILengthPrefixedStreamWriter CreateWriter()
        {
            var writeStream = new AnonymousPipeClientStream(PipeDirection.Out, Payload.WritePipe);
            return new SyncLengthPrefixedStreamWriter(writeStream, Payload.ProcessUniqueId + " - SubprocessWrite");
        }

        internal override IEnumerable<Task> GetShutdownEvents()
        {
            if (string.IsNullOrWhiteSpace(Payload.ShutdownEvent))
                yield break;

            var h = SafeHandleUtilities.CreateWaitHandleFromString(Payload.ShutdownEvent);
            yield return h.WaitAsync();
        }
    }
}
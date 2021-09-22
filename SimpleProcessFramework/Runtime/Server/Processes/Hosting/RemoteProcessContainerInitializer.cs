using Spfx.Io;
using Spfx.Reflection;
using Spfx.Runtime.Server.Processes.Ipc;
using Spfx.Utilities;

namespace Spfx.Runtime.Server.Processes.Hosting
{
    internal abstract class RemoteProcessContainerInitializer : ProcessContainerInitializer
    {
        protected RemoteProcessContainerInitializer(ProcessSpawnPunchPayload payload, ITypeResolver typeResolver)
            : base(payload, typeResolver)
        {
        }

        internal abstract ILengthPrefixedStreamWriter CreateWriter();
        internal abstract ILengthPrefixedStreamReader CreateReader();

        internal override ISubprocessConnector CreateConnector(ProcessContainer owner)
        {
            using var disposeBag = new DisposeBag();

            var streamReader = disposeBag.Add(CreateReader());
            var streamWriter = disposeBag.Add(CreateWriter());
            var connector = disposeBag.Add(new SubprocessIpcConnector(owner, streamReader, streamWriter, TypeResolver));
            disposeBag.ReleaseAll();
            return connector;
        }
    }
}
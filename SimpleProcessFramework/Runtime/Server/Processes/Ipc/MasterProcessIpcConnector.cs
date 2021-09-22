using Spfx.Io;
using Spfx.Reflection;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes.Ipc
{
    internal class MasterProcessIpcConnector : IpcConnector
    {
        public MasterProcessIpcConnector(GenericRemoteTargetHandle owner, ILengthPrefixedStreamReader reader, ILengthPrefixedStreamWriter writer, ITypeResolver typeResolver)
            : base(owner, reader, writer, typeResolver, owner.ProcessUniqueId)
        {
        }

        protected override async Task DoInitialize()
        {
            await ReceiveCode(InterprocessFrameType.Handshake1).ConfigureAwait(false);
            await Owner.CompleteInitialization().ConfigureAwait(false);
            SendCode(InterprocessFrameType.Handshake2);
            await ReceiveCode(InterprocessFrameType.Handshake3).ConfigureAwait(false);
        }
    }
}
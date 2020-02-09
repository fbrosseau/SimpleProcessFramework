using System.Threading.Tasks;
using Spfx.Io;
using Spfx.Reflection;

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
            await ReceiveCode(InterprocessFrameType.Handshake1);
            await Owner.CompleteInitialization();
            SendCode(InterprocessFrameType.Handshake2);
            await ReceiveCode(InterprocessFrameType.Handshake3);
        }
    }
}
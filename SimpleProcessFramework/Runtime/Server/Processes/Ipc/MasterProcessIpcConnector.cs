using System.Threading.Tasks;
using Spfx.Io;
using Spfx.Reflection;

namespace Spfx.Runtime.Server.Processes.Ipc
{
    internal class MasterProcessIpcConnector : IpcConnector
    {
        public MasterProcessIpcConnector(GenericRemoteTargetHandle owner, IRemoteProcessInitializer remoteProcessHandles, ITypeResolver typeResolver)
            : base(
                  owner,
                  PipeWriterFactory.CreateReader(remoteProcessHandles.ReadStream, owner.ProcessUniqueId + " - MasterRead"),
                  PipeWriterFactory.CreateWriter(remoteProcessHandles.WriteStream, owner.ProcessUniqueId + " - MasterWrite"),
                  typeResolver)
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
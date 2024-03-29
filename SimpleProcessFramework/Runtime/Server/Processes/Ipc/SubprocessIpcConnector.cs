﻿using Spfx.Io;
using Spfx.Reflection;
using Spfx.Runtime.Messages;
using Spfx.Runtime.Server.Processes.Hosting;
using System;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server.Processes.Ipc
{
    internal class SubprocessIpcConnector : IpcConnector, ISubprocessConnector
    {
        public SubprocessIpcConnector(ProcessContainer owner, ILengthPrefixedStreamReader readStream, ILengthPrefixedStreamWriter writeStream, ITypeResolver typeResolver)
            : base(owner, readStream, writeStream, typeResolver, owner.LocalProcessUniqueId)
        {
        }

        public ValueTask<IInterprocessClientChannel> GetClientInfo(string uniqueId)
        {
            throw new NotImplementedException();
        }

        protected override async Task DoInitialize()
        {
            SendCode(InterprocessFrameType.Handshake1);
            await ReceiveCode(InterprocessFrameType.Handshake2).ConfigureAwait(false);
            await Owner.CompleteInitialization().ConfigureAwait(false);
            SendCode(InterprocessFrameType.Handshake3);
        }

        void IMessageCallbackChannel.HandleMessage(string connectionId, IInterprocessMessage msg)
        {
            var wrapped = WrappedInterprocessMessage.Wrap(msg, BinarySerializer);
            wrapped.SourceConnectionId = connectionId;
            ForwardMessage(wrapped);
        }
    }
}

﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Spfx.Runtime.Messages;

namespace Spfx.Runtime.Server.Processes.Ipc
{
    internal interface IIpcConnectorListener
    {
        Task CompleteInitialization(CancellationToken ct = default);
        void OnMessageReceived(WrappedInterprocessMessage msg);
        void OnTeardownRequestReceived();

        void OnRemoteEndLost(string msg, Exception ex = null);
    }
}

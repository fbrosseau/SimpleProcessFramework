﻿using Spfx.Utilities.Threading;

namespace Spfx.Runtime.Server
{
    public interface IClientConnectionManager : IAsyncDestroyable
    {
        void AddListener(IConnectionListener listener);
        void RemoveListener(IConnectionListener listener);

        void RegisterClientChannel(IInterprocessClientChannel channel);
        IInterprocessClientChannel GetClientChannel(long connectionId, bool mustExist);
    }
}
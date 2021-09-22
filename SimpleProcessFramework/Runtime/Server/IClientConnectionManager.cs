using Spfx.Utilities.Threading;
using System.Collections.Generic;
using System.Net;

namespace Spfx.Runtime.Server
{
    public interface IClientConnectionManager : IAsyncDestroyable
    {
        void AddListener(IConnectionListener listener);
        void RemoveListener(IConnectionListener listener);

        void RegisterClientChannel(IInterprocessClientChannel channel);
        IInterprocessClientChannel GetClientChannel(string connectionId, bool mustExist);

        List<EndPoint> GetListenEndpoints();
        List<EndPoint> GetConnectEndpoints();
    }
}
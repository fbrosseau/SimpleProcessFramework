using System.Net;

namespace Spfx.Runtime.Server.Listeners
{
    public interface IExternalConnectionsListener
    {
        EndPoint ListenEndpoint { get; }
        EndPoint ConnectEndpoint { get; }
    }
}

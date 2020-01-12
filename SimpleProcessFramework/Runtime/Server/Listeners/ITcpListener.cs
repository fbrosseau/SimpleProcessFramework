using System.Net;

namespace Spfx.Runtime.Server.Listeners
{
    public interface ITcpListener : IExternalConnectionsListener
    {
        new IPEndPoint ListenEndpoint { get; }
    }
}

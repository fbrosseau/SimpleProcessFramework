using System.Net.Sockets;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Exceptions
{
    [DataContract]
    public class ProxySocketConnectionFailedException : ProxyConnectionFailedException
    {
        [DataMember]
        public SocketError SocketError { get; }

        public ProxySocketConnectionFailedException(SocketException ex)
            : base(ex.Message, ex)
        {
            SocketError = ex.SocketErrorCode;
        }
    }
}
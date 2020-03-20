using System.Runtime.Serialization;
using System.Security.Authentication;

namespace Spfx.Runtime.Exceptions
{
    [DataContract]
    public class ProxyTlsConnectionFailedException : ProxyConnectionFailedException
    {
        public ProxyTlsConnectionFailedException(AuthenticationException ex)
        {
        }
    }
}
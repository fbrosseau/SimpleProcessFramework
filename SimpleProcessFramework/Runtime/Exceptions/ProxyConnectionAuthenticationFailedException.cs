using System.Runtime.Serialization;

namespace Spfx.Runtime.Exceptions
{
    [DataContract]
    public class ProxyConnectionAuthenticationFailedException : ProxyConnectionFailedException
    {
        public ProxyConnectionAuthenticationFailedException(string msg)
            : base(msg)
        {
        }
    }
}
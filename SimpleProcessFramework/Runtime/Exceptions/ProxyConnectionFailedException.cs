using System;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Exceptions
{
    [DataContract]
    public class ProxyConnectionFailedException : SerializableException
    {
        public ProxyConnectionFailedException()
        {
        }

        public ProxyConnectionFailedException(string msg)
            : base(msg)
        {
        }

        public ProxyConnectionFailedException(string msg, Exception innerEx)
            : base(msg, innerEx)
        {
        }
    }
}
using System;

namespace Spfx.Runtime.Exceptions
{
    public class InvalidClientConnectionProtocolException : Exception
    {
        public InvalidClientConnectionProtocolException(string msg)
            : base(msg)
        {
        }
    }
}

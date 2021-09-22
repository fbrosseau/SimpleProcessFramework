using System;

namespace Spfx.Runtime.Exceptions
{
    public class InvalidProxyInterfaceException : Exception
    {
        public InvalidProxyInterfaceException(string message)
            : base(message)
        {
        }
    }
}

using System;

namespace SimpleProcessFramework.Runtime.Exceptions
{
    public class InvalidProxyInterfaceException : Exception
    {
        public InvalidProxyInterfaceException(string message) 
            : base(message)
        {
        }
    }
}

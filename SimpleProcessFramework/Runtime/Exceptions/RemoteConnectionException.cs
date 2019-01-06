using System;

namespace SimpleProcessFramework.Runtime.Exceptions
{
    public class RemoteConnectionException : Exception
    {
        public RemoteConnectionException(string msg)
            : base(msg)
        {
        }
    }
}
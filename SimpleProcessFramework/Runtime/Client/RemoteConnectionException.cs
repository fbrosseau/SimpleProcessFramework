using System;

namespace SimpleProcessFramework.Runtime.Client
{
    public class RemoteConnectionException : Exception
    {
        public RemoteConnectionException(string msg)
            : base(msg)
        {
        }
    }
}
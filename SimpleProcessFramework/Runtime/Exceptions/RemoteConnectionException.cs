using System;

namespace Spfx.Runtime.Exceptions
{
    public class RemoteConnectionException : Exception
    {
        public RemoteConnectionException(string msg)
            : base(msg)
        {
        }
    }
}
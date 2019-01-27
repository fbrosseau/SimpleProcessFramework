using System;

namespace Spfx.Runtime.Server
{
    public class InvalidProcessParametersException : Exception
    {
        public InvalidProcessParametersException(string message) 
            : base(message)
        {
        }
    }
}

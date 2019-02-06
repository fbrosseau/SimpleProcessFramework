using System;

namespace Spfx.Runtime.Exceptions
{
    public class InvalidProcessParametersException : Exception
    {
        public InvalidProcessParametersException(string message) 
            : base(message)
        {
        }
    }
}

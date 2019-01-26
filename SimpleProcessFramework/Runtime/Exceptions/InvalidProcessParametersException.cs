using System;

namespace SimpleProcessFramework.Runtime.Server
{
    public class InvalidProcessParametersException : Exception
    {
        public InvalidProcessParametersException(string message) 
            : base(message)
        {
        }
    }
}

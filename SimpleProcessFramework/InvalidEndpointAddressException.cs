using System;

namespace Spfx
{
    public class InvalidEndpointAddressException : FormatException
    {
        public InvalidEndpointAddressException(string userInput)
            : base("Invalid address format: " + userInput)
        {
        }
    }
}

using System;

namespace SimpleProcessFramework.Runtime.Exceptions
{
    public class EndpointNotFoundException : Exception
    {
        private ProcessEndpointAddress destination;

        public EndpointNotFoundException(ProcessEndpointAddress destination)
        {
            this.destination = destination;
        }
    }
    public class EndpointAlreadyExistsException : Exception
    {
        private string destination;

        public EndpointAlreadyExistsException(string destination)
        {
            this.destination = destination;
        }
    }
}
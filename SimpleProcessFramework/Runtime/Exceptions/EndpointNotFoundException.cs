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
}
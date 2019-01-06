using System;

namespace SimpleProcessFramework.Runtime.Exceptions
{
    public class ProcessNotFoundException : Exception
    {
        private ProcessEndpointAddress destination;

        public ProcessNotFoundException(string targetProcess)
        {
        }
    }
}
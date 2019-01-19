using System;

namespace SimpleProcessFramework.Runtime.Exceptions
{
    public class ProcessNotFoundException : Exception
    {
        public ProcessNotFoundException(string targetProcess)
        {
        }
    }
}
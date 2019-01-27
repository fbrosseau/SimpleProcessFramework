using System;

namespace Spfx.Runtime.Exceptions
{
    public class ProcessNotFoundException : Exception
    {
        public ProcessNotFoundException(string targetProcess)
        {
        }
    }
}
using System;

namespace Spfx.Runtime.Exceptions
{
    public class ProcessInitializationException : Exception
    {
        public ProcessInitializationException()
            : this("The remote process has exited before completing its startup")
        {
        }

        public ProcessInitializationException(string message, Exception innerException = null)
            : base(message, innerException)
        {
        }

        public string ProcessOutput { get; set; }
    }
}

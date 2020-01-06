using System;

namespace Spfx.Runtime.Exceptions
{
    public class SubprocessLostException : SerializableException
    {
        public SubprocessLostException(string msg, Exception innerEx = null)
            : base(msg, innerEx)
        {
        }
    }
}
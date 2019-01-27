using System;

namespace Spfx.Runtime.Exceptions
{
    public class BadMethodInvocationException : Exception
        {
            public BadMethodInvocationException(string msg)
                : base(msg)
            {
            }
        }
}
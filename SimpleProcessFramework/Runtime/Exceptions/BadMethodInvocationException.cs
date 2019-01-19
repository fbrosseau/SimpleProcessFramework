using System;

namespace SimpleProcessFramework.Runtime.Exceptions
{
    public class BadMethodInvocationException : Exception
        {
            public BadMethodInvocationException(string msg)
                : base(msg)
            {
            }
        }
}
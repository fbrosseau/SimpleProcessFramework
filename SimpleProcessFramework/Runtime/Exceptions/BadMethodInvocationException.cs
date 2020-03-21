using Spfx.Utilities;

namespace Spfx.Runtime.Exceptions
{
    public class BadMethodInvocationException : SerializableException
    {
        public BadMethodInvocationException(string msg)
            : base(msg)
        {
            BadCodeAssert.Assert(msg);
        }
    }
}
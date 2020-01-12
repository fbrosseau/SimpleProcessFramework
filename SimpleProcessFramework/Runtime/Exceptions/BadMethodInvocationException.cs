namespace Spfx.Runtime.Exceptions
{
    public class BadMethodInvocationException : SerializableException
    {
        public BadMethodInvocationException(string msg)
            : base(msg)
        {
        }
    }
}
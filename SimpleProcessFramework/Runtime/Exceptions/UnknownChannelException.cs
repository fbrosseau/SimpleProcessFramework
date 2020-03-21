using System.Runtime.Serialization;

namespace Spfx.Runtime.Exceptions
{
    [DataContract]
    public class UnknownChannelException : SerializableException
    {
        public UnknownChannelException(string msg)
            : base(msg)
        {
        }
    }
}

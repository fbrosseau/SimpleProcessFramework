using Spfx.Utilities;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Exceptions
{
    [DataContract]
    public class BadDataContractException : SerializableException
    {
        public BadDataContractException(string message)
            : base(message)
        {
            BadCodeAssert.Assert(message);
        }
    }
}

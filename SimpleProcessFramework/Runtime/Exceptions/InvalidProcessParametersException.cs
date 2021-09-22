using System.Runtime.Serialization;

namespace Spfx.Runtime.Exceptions
{
    [DataContract]
    public class MissingSubprocessExecutableException : InvalidProcessParametersException
    {
        [DataMember]
        public string Filename { get; }

        public MissingSubprocessExecutableException(string filename)
            : base("The target executable does not exist: " + filename)
        {
            Filename = filename;
        }
    }

    [DataContract]
    public class InvalidProcessParametersException : SerializableException
    {
        public InvalidProcessParametersException(string message)
            : base(message)
        {
        }
    }
}

using System.Runtime.Serialization;

namespace Spfx.Runtime.Exceptions
{
    [DataContract]
    public class ProcessAlreadyExistsException : SerializableException
    {
        [DataMember]
        public string ProcessId { get; }

        public ProcessAlreadyExistsException(string processId)
            : base("Process already exists: " + processId)
        {
            ProcessId = processId;
        }
    }
}
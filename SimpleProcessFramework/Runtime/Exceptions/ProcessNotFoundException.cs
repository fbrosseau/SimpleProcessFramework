using System.Runtime.Serialization;

namespace Spfx.Runtime.Exceptions
{
    [DataContract]
    public class ProcessNotFoundException : SerializableException
    {
        [DataMember]
        public string ProcessId { get; }

        public ProcessNotFoundException(string processId)
            : base("Process not found: " + processId)
        {
            ProcessId = processId;
        }
    }
}
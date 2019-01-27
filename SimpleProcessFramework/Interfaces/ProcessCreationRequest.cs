using System.Runtime.Serialization;

namespace Spfx.Interfaces
{
    [DataContract]
    public class ProcessCreationRequest
    {
        [DataMember]
        public bool MustCreateNew { get; set; } = true;

        [DataMember]
        public ProcessCreationInfo ProcessInfo { get; set; }
    }
}

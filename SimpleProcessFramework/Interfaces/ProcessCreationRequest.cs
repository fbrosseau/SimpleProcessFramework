using System;
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

        internal void EnsureIsValid()
        {
            if (ProcessInfo is null)
                throw new InvalidOperationException("ProcessInfo cannot be null");

            ProcessInfo.EnsureIsValid();
        }
    }
}

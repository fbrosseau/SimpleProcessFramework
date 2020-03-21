using Spfx.Utilities;
using System;
using System.Runtime.Serialization;

namespace Spfx.Interfaces
{
    [DataContract]
    public class ProcessCreationRequest
    {
        [DataMember]
        public ProcessCreationOptions Options { get; set; } = ProcessCreationOptions.ThrowIfExists;

        [DataMember]
        public ProcessCreationInfo ProcessInfo { get; set; }

        internal void EnsureIsValid()
        {
            if (ProcessInfo is null)
                BadCodeAssert.ThrowInvalidOperation("ProcessInfo cannot be null");

            ProcessInfo.EnsureIsValid();
        }
    }
}

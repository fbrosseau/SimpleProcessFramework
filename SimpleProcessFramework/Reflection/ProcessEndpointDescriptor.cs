using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SimpleProcessFramework.Reflection
{
    [DataContract]
    public class ProcessEndpointDescriptor
    {
        [DataMember]
        public IReadOnlyCollection<ProcessEndpointMethodDescriptor> Methods { get; set; }
    }
}
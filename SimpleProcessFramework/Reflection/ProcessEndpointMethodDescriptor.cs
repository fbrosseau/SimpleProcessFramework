using System.Runtime.Serialization;

namespace SimpleProcessFramework.Reflection
{
    public class ProcessEndpointMethodDescriptor
    {
        [DataMember]
        public ReflectedMethodInfo Method { get; set; }

        [DataMember]
        public int MethodId { get; set; }

        [DataMember]
        public bool IsCancellable { get; set; }
    }
}
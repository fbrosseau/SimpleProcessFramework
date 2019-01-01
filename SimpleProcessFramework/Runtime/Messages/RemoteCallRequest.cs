using SimpleProcessFramework.Reflection;
using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace SimpleProcessFramework.Runtime.Messages
{
    [DataContract]
    public sealed class RemoteCallRequest : RemoteInvocationRequest
    {
        [DataMember]
        public int MethodId { get; set; }

        [DataMember]
        public object[] Args { get; set; }

        public object[] GetArgsOrEmpty() => Args ?? Array.Empty<object>();

        internal static class Reflection
        {
            public static MethodInfo GetArgsOrEmptyMethod => typeof(RemoteCallRequest).FindUniqueMethod(nameof(GetArgsOrEmpty));
            public static MethodInfo Get_MethodIdMethod => typeof(RemoteCallRequest)
                .GetProperty(nameof(MethodId)).GetGetMethod();
        }
    }
}

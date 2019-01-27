using Spfx.Reflection;
using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    [DataContract]
    public sealed class RemoteCallRequest : RemoteInvocationRequest
    {
        [DataMember]
        public int MethodId { get; set; }

        [DataMember]
        public string MethodName { get; set; }

        [DataMember]
        public object[] Arguments { get; set; }

        public object[] GetArgsOrEmpty() => Arguments ?? Array.Empty<object>();
        public override bool ExpectResponse => true;

        internal static class Reflection
        {
            public static MethodInfo GetArgsOrEmptyMethod => typeof(RemoteCallRequest).FindUniqueMethod(nameof(GetArgsOrEmpty));
            public static MethodInfo Get_MethodIdMethod => typeof(RemoteCallRequest)
                .GetProperty(nameof(MethodId)).GetGetMethod();
        }
    }
}

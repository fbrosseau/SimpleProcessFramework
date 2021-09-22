using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace Spfx.Runtime.Messages
{
    internal interface IRemoteCallRequest
    {
        int MethodId { get; set; }
        string MethodName { get; }

        int ArgsCount { get; }
        T GetArg<T>(int index);
    }

    [DataContract]
    public sealed class RemoteCallRequest : RemoteInvocationRequest, IRemoteCallRequest
    {
        public override bool ExpectResponse => true;

        [DataMember]
        public int MethodId { get; set; }

        [DataMember]
        public string MethodName { get; set; }

        [DataMember]
        public object[] Arguments { get; set; }

        public T GetArg<T>(int index)
        {
            return (T)Arguments[index];
        }

        public int ArgsCount => Arguments?.Length ?? 0;

        public override string GetTinySummaryString()
            => nameof(RemoteCallRequest) + ":" + MethodName + "(#" + CallId + ")";

        internal static class Reflection
        {
            public static MethodInfo Get_MethodIdMethod => typeof(IRemoteCallRequest)
                .GetProperty(nameof(MethodId)).GetGetMethod();
            public static MethodInfo GetArgMethod(Type resultType) => typeof(IRemoteCallRequest)
                .GetMethod(nameof(GetArg))
                .MakeGenericMethod(resultType);
        }
    }
}

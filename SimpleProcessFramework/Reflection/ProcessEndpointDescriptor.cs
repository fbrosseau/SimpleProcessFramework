using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

namespace SimpleProcessFramework.Reflection
{
    [DataContract]
    public class ProcessEndpointDescriptor
    {
        [DataMember]
        public IReadOnlyCollection<ProcessEndpointMethodDescriptor> Methods { get; set; }

        public static ProcessEndpointDescriptor CreateFromCurrentProcess(Type type)
        {
            var methodDescriptors = new List<ProcessEndpointMethodDescriptor>();
            foreach (var m in type.GetMethods())
            {
                methodDescriptors.Add(new ProcessEndpointMethodDescriptor
                {
                    Method = new ReflectedMethodInfo(m),
                    MethodId = methodDescriptors.Count,
                    IsCancellable = m.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken))
                });
            }

            return new ProcessEndpointDescriptor
            {
                Methods = methodDescriptors.ToArray()
            };
        }
    }
}